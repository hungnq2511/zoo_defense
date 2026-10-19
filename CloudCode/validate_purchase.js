/**
 * Zoo Defense — server-side purchase check (Unity Cloud Code script "validate_purchase").
 *
 * The game calls this before granting a paid product. For the App Store it verifies the StoreKit 2 signed
 * transaction (JWS) the phone received:
 *   1. the certificate chain in the JWS header ends at Apple Root CA - G3 (pinned SHA-256 fingerprint),
 *      every certificate is signed by the next one, and the leaf / intermediate carry Apple's receipt OIDs;
 *   2. the ES256 signature over header.payload is valid for the leaf key;
 *   3. the transaction is for this app (bundle id), this product and this transaction id, and not revoked;
 *   4. the transaction id was never used by ANOTHER player (ledger in Cloud Save game data "iap_ledger").
 * Google Play: not verified yet (needs a Google service account) -> answered "unverified".
 *
 * Cloud Code scripts have no crypto module, so SHA-256/384, ECDSA (P-256, P-384) and the X.509 parsing are
 * implemented below in plain JavaScript (BigInt), checked against Node's crypto (hashes, signatures, test chains).
 *
 * Params: store ("apple" | "google"), productId, transactionId, jws (apple), receipt (google)
 * Returns: { valid: bool, reason: string, environment?: string }
 */

const BUNDLE_ID = "com.hungnq.zoodefense";
// Apple Root CA - G3, SHA-256 (https://www.apple.com/certificateauthority/)
const APPLE_ROOT_G3_SHA256 = "63:34:3A:BF:B8:9A:6A:03:EB:B5:7E:9B:3F:5F:A7:BE:7C:4F:5C:75:6F:30:17:B3:A8:C4:88:C3:65:3E:91:79";
// DER of OID 1.2.840.113635.100.6.11.1 (leaf: receipt signing) and 1.2.840.113635.100.6.2.1 (WWDR intermediate)
const OID_LEAF = Buffer.from("060a2a864886f76364060b01", "hex");
const OID_INTERMEDIATE = Buffer.from("060a2a864886f76364060201", "hex");
const LEDGER_ID = "iap_ledger";

module.exports = async ({ params, context, logger }) => {
  const store = String(params.store || "").toLowerCase();
  if (store === "google") return { valid: true, reason: "unverified_google" };
  if (store !== "apple") return { valid: false, reason: "unknown_store" };

  const check = verifyAppleJws(params.jws, params.productId, params.transactionId, APPLE_ROOT_G3_SHA256);
  if (!check.valid) {
    logger.warning("purchase rejected: " + check.reason, { productId: params.productId, transactionId: params.transactionId });
    return check;
  }

  // One transaction can only ever be redeemed by one player.
  const { DataApi } = require("@unity-services/cloud-save-1.4");
  const api = new DataApi({ accessToken: context.serviceToken });
  const key = "tx_" + String(params.transactionId).replace(/[^A-Za-z0-9_-]/g, "");
  const existing = await api.getCustomItems(context.projectId, LEDGER_ID, [key]);
  const owner = existing.data && existing.data.results && existing.data.results[0] ? existing.data.results[0].value : null;
  if (owner && owner.playerId && owner.playerId !== context.playerId) {
    logger.warning("transaction reused by another player", { transactionId: params.transactionId });
    return { valid: false, reason: "used_by_other_player" };
  }
  if (!owner) {
    await api.setCustomItem(context.projectId, LEDGER_ID, {
      key,
      value: { playerId: context.playerId, productId: params.productId, at: new Date().toISOString(), env: check.environment },
    });
  }
  return check;
};

// ===================================================================== Apple JWS check (pure, no network)

/** rootFingerprint is a parameter so the local tests can pin their own test root. */
function verifyAppleJws(jws, productId, transactionId, rootFingerprint) {
  try {
    if (typeof jws !== "string") return { valid: false, reason: "missing_jws" };
    const parts = jws.split(".");
    if (parts.length !== 3) return { valid: false, reason: "bad_jws" };
    const header = JSON.parse(b64url(parts[0]).toString("utf8"));
    if (header.alg !== "ES256" || !Array.isArray(header.x5c) || header.x5c.length < 3) return { valid: false, reason: "bad_header" };

    const certs = header.x5c.map((c) => parseCert(Buffer.from(c, "base64")));
    const leaf = certs[0], intermediate = certs[1], root = certs[certs.length - 1];
    if (fingerprint256(root.der) !== rootFingerprint) return { valid: false, reason: "untrusted_root" };
    for (let i = 0; i < certs.length - 1; i++) {
      if (!verifyCertSignature(certs[i], certs[i + 1].key)) return { valid: false, reason: "broken_chain" };
    }
    if (!verifyCertSignature(root, root.key)) return { valid: false, reason: "bad_root" };
    if (!leaf.der.includes(OID_LEAF) || !intermediate.der.includes(OID_INTERMEDIATE)) return { valid: false, reason: "not_apple_receipt_cert" };

    const sig = b64url(parts[2]);
    if (leaf.key.curve !== P256 || sig.length !== 64) return { valid: false, reason: "bad_signature" };
    const r = bytesToBig(sig.subarray(0, 32)), s = bytesToBig(sig.subarray(32));
    if (!ecdsaVerify(leaf.key, sha256(Buffer.from(parts[0] + "." + parts[1], "ascii")), r, s)) return { valid: false, reason: "bad_signature" };

    const tx = JSON.parse(b64url(parts[1]).toString("utf8"));
    if (tx.bundleId !== BUNDLE_ID) return { valid: false, reason: "wrong_app" };
    if (tx.productId !== productId) return { valid: false, reason: "wrong_product" };
    if (String(tx.transactionId) !== String(transactionId)) return { valid: false, reason: "wrong_transaction" };
    if (tx.revocationDate) return { valid: false, reason: "revoked" };
    return { valid: true, reason: "ok", environment: tx.environment || "" };
  } catch (e) {
    return { valid: false, reason: "error: " + e.message };
  }
}

function b64url(s) {
  return Buffer.from(String(s).replace(/-/g, "+").replace(/_/g, "/"), "base64");
}

function fingerprint256(der) {
  return Array.from(sha256(der)).map((b) => b.toString(16).toUpperCase().padStart(2, "0")).join(":");
}

// ===================================================================== DER / X.509

function readTlv(buf, pos) {
  const tag = buf[pos];
  let len = buf[pos + 1], off = pos + 2;
  if (len & 0x80) {
    const n = len & 0x7f;
    len = 0;
    for (let i = 0; i < n; i++) len = len * 256 + buf[off + i];
    off += n;
  }
  return { tag, start: pos, contentStart: off, end: off + len };
}

function children(buf, tlv) {
  const out = [];
  let p = tlv.contentStart;
  while (p < tlv.end) { const c = readTlv(buf, p); out.push(c); p = c.end; }
  return out;
}

const OID_SHA256_ECDSA = "2a8648ce3d040302", OID_SHA384_ECDSA = "2a8648ce3d040303";
const OID_P256 = "2a8648ce3d030107", OID_P384 = "2b81040022";

/** Certificate -> { der, tbs, sigAlg, r, s, key: { curve, x, y } } (EC keys only). */
function parseCert(der) {
  const cert = readTlv(der, 0);
  const [tbsTlv, algTlv, sigTlv] = children(der, cert);
  const tbs = der.subarray(tbsTlv.start, tbsTlv.end);
  const sigAlg = der.subarray(children(der, algTlv)[0].contentStart, children(der, algTlv)[0].end).toString("hex");
  // BIT STRING: first content byte = unused bits (0), then SEQUENCE { r INTEGER, s INTEGER }
  const sigSeq = readTlv(der, sigTlv.contentStart + 1);
  const [rT, sT] = children(der, sigSeq);
  const r = bytesToBig(der.subarray(rT.contentStart, rT.end)), s = bytesToBig(der.subarray(sT.contentStart, sT.end));

  // tbs fields: [0]version?, serial, sigAlg, issuer, validity, subject, spki, ...
  const fields = children(der, tbsTlv);
  const idx = fields[0].tag === 0xa0 ? 6 : 5;
  const spki = fields[idx];
  const [algId, keyBits] = children(der, spki);
  const algParts = children(der, algId);
  const curveOid = der.subarray(algParts[1].contentStart, algParts[1].end).toString("hex");
  const curve = curveOid === OID_P256 ? P256 : curveOid === OID_P384 ? P384 : null;
  if (!curve) throw new Error("unsupported key");
  const point = der.subarray(keyBits.contentStart + 1, keyBits.end);   // 04 || X || Y
  if (point[0] !== 4) throw new Error("compressed key");
  const size = (point.length - 1) / 2;
  const key = { curve, x: bytesToBig(point.subarray(1, 1 + size)), y: bytesToBig(point.subarray(1 + size)) };
  return { der, tbs, sigAlg, r, s, key };
}

function verifyCertSignature(cert, issuerKey) {
  const hash = cert.sigAlg === OID_SHA256_ECDSA ? sha256(cert.tbs) : cert.sigAlg === OID_SHA384_ECDSA ? sha384(cert.tbs) : null;
  if (!hash) return false;
  return ecdsaVerify(issuerKey, hash, cert.r, cert.s);
}

function bytesToBig(bytes) {
  let hex = Buffer.from(bytes).toString("hex");
  return hex.length ? BigInt("0x" + hex) : 0n;
}

// ===================================================================== ECDSA over NIST curves (a = -3)

function curveOf(pHex, nHex, bHex, gxHex, gyHex, bits) {
  return { p: BigInt("0x" + pHex), n: BigInt("0x" + nHex), b: BigInt("0x" + bHex), gx: BigInt("0x" + gxHex), gy: BigInt("0x" + gyHex), bits };
}
const P256 = curveOf(
  "ffffffff00000001000000000000000000000000ffffffffffffffffffffffff",
  "ffffffff00000000ffffffffffffffffbce6faada7179e84f3b9cac2fc632551",
  "5ac635d8aa3a93e7b3ebbd55769886bc651d06b0cc53b0f63bce3c3e27d2604b",
  "6b17d1f2e12c4247f8bce6e563a440f277037d812deb33a0f4a13945d898c296",
  "4fe342e2fe1a7f9b8ee7eb4a7c0f9e162bce33576b315ececbb6406837bf51f5", 256);
const P384 = curveOf(
  "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffeffffffff0000000000000000ffffffff",
  "ffffffffffffffffffffffffffffffffffffffffffffffffc7634d81f4372ddf581a0db248b0a77aecec196accc52973",
  "b3312fa7e23ee7e4988e056be3f82d19181d9c6efe8141120314088f5013875ac656398d8a2ed19d2a85c8edd3ec2aef",
  "aa87ca22be8b05378eb1c71ef320ad746e1d3b628ba79b9859f741e082542a385502f25dbf55296c3a545e3872760ab7",
  "3617de4a96262c6f5d9e98bf9292dc29f8f41dbd289a147ce9da3113b5f0b8c00a60b1ce1d7e819d7a431d7c90ea0e5f", 384);

function mod(a, m) { const r = a % m; return r >= 0n ? r : r + m; }
function modPow(b, e, m) { let r = 1n; b = mod(b, m); while (e > 0n) { if (e & 1n) r = (r * b) % m; b = (b * b) % m; e >>= 1n; } return r; }
function inv(a, m) { return modPow(mod(a, m), m - 2n, m); }   // m is prime

// Jacobian coordinates; null = point at infinity
function jDouble(P, c) {
  if (!P || P.y === 0n) return null;
  const p = c.p;
  const z2 = (P.z * P.z) % p;
  const m3 = (3n * mod((P.x - z2) * (P.x + z2), p)) % p;           // a = -3
  const y2 = (P.y * P.y) % p;
  const s = (4n * P.x * y2) % p;
  const x = mod(m3 * m3 - 2n * s, p);
  const y = mod(m3 * (s - x) - 8n * y2 * y2, p);
  const z = (2n * P.y * P.z) % p;
  return { x, y, z };
}

function jAdd(P, Q, c) {
  if (!P) return Q;
  if (!Q) return P;
  const p = c.p;
  const z1z1 = (P.z * P.z) % p, z2z2 = (Q.z * Q.z) % p;
  const u1 = (P.x * z2z2) % p, u2 = (Q.x * z1z1) % p;
  const s1 = (P.y * Q.z * z2z2) % p, s2 = (Q.y * P.z * z1z1) % p;
  if (u1 === u2) return s1 === s2 ? jDouble(P, c) : null;
  const h = mod(u2 - u1, p), r = mod(s2 - s1, p);
  const h2 = (h * h) % p, h3 = (h2 * h) % p, u1h2 = (u1 * h2) % p;
  const x = mod(r * r - h3 - 2n * u1h2, p);
  const y = mod(r * (u1h2 - x) - s1 * h3, p);
  const z = (h * P.z * Q.z) % p;
  return { x, y, z };
}

function jMul(P, k, c) {
  let R = null;
  for (let i = BigInt(k.toString(2).length - 1); i >= 0n; i--) {
    R = jDouble(R, c);
    if ((k >> i) & 1n) R = jAdd(R, P, c);
  }
  return R;
}

function onCurve(x, y, c) {
  const p = c.p;
  return mod(y * y - (x * x * x - 3n * x + c.b), p) === 0n;
}

function ecdsaVerify(key, hash, r, s) {
  const c = key.curve;
  if (r <= 0n || s <= 0n || r >= c.n || s >= c.n) return false;
  if (!onCurve(key.x, key.y, c)) return false;
  let e = bytesToBig(hash);
  const hashBits = hash.length * 8;
  if (hashBits > c.bits) e >>= BigInt(hashBits - c.bits);
  const w = inv(s, c.n);
  const u1 = (e * w) % c.n, u2 = (r * w) % c.n;
  const R = jAdd(jMul({ x: c.gx, y: c.gy, z: 1n }, u1, c), jMul({ x: key.x, y: key.y, z: 1n }, u2, c), c);
  if (!R) return false;
  const zi = inv(R.z, c.p);
  const x = (R.x * zi * zi) % c.p;
  return x % c.n === r;
}

// ===================================================================== SHA-256 / SHA-384

const K256 = [
  0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
  0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
  0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
  0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
  0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
  0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2];

function sha256(data) {
  data = Buffer.from(data);
  const len = data.length, total = Math.ceil((len + 9) / 64) * 64;
  const m = Buffer.alloc(total);
  data.copy(m);
  m[len] = 0x80;
  m.writeUInt32BE(Math.floor(len / 0x20000000), total - 8);
  m.writeUInt32BE((len * 8) >>> 0, total - 4);
  const H = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
  const W = new Array(64);
  const rotr = (x, n) => (x >>> n) | (x << (32 - n));
  for (let off = 0; off < total; off += 64) {
    for (let i = 0; i < 16; i++) W[i] = m.readUInt32BE(off + i * 4);
    for (let i = 16; i < 64; i++) {
      const s0 = rotr(W[i - 15], 7) ^ rotr(W[i - 15], 18) ^ (W[i - 15] >>> 3);
      const s1 = rotr(W[i - 2], 17) ^ rotr(W[i - 2], 19) ^ (W[i - 2] >>> 10);
      W[i] = (W[i - 16] + s0 + W[i - 7] + s1) | 0;
    }
    let [a, b, c, d, e, f, g, h] = H;
    for (let i = 0; i < 64; i++) {
      const S1 = rotr(e, 6) ^ rotr(e, 11) ^ rotr(e, 25);
      const ch = (e & f) ^ (~e & g);
      const t1 = (h + S1 + ch + K256[i] + W[i]) | 0;
      const S0 = rotr(a, 2) ^ rotr(a, 13) ^ rotr(a, 22);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) | 0;
      h = g; g = f; f = e; e = (d + t1) | 0; d = c; c = b; b = a; a = (t1 + t2) | 0;
    }
    H[0] = (H[0] + a) | 0; H[1] = (H[1] + b) | 0; H[2] = (H[2] + c) | 0; H[3] = (H[3] + d) | 0;
    H[4] = (H[4] + e) | 0; H[5] = (H[5] + f) | 0; H[6] = (H[6] + g) | 0; H[7] = (H[7] + h) | 0;
  }
  const out = Buffer.alloc(32);
  H.forEach((v, i) => out.writeUInt32BE(v >>> 0, i * 4));
  return out;
}

const K512 = [
  "428a2f98d728ae22", "7137449123ef65cd", "b5c0fbcfec4d3b2f", "e9b5dba58189dbbc", "3956c25bf348b538", "59f111f1b605d019", "923f82a4af194f9b", "ab1c5ed5da6d8118",
  "d807aa98a3030242", "12835b0145706fbe", "243185be4ee4b28c", "550c7dc3d5ffb4e2", "72be5d74f27b896f", "80deb1fe3b1696b1", "9bdc06a725c71235", "c19bf174cf692694",
  "e49b69c19ef14ad2", "efbe4786384f25e3", "0fc19dc68b8cd5b5", "240ca1cc77ac9c65", "2de92c6f592b0275", "4a7484aa6ea6e483", "5cb0a9dcbd41fbd4", "76f988da831153b5",
  "983e5152ee66dfab", "a831c66d2db43210", "b00327c898fb213f", "bf597fc7beef0ee4", "c6e00bf33da88fc2", "d5a79147930aa725", "06ca6351e003826f", "142929670a0e6e70",
  "27b70a8546d22ffc", "2e1b21385c26c926", "4d2c6dfc5ac42aed", "53380d139d95b3df", "650a73548baf63de", "766a0abb3c77b2a8", "81c2c92e47edaee6", "92722c851482353b",
  "a2bfe8a14cf10364", "a81a664bbc423001", "c24b8b70d0f89791", "c76c51a30654be30", "d192e819d6ef5218", "d69906245565a910", "f40e35855771202a", "106aa07032bbd1b8",
  "19a4c116b8d2d0c8", "1e376c085141ab53", "2748774cdf8eeb99", "34b0bcb5e19b48a8", "391c0cb3c5c95a63", "4ed8aa4ae3418acb", "5b9cca4f7763e373", "682e6ff3d6b2b8a3",
  "748f82ee5defb2fc", "78a5636f43172f60", "84c87814a1f0ab72", "8cc702081a6439ec", "90befffa23631e28", "a4506cebde82bde9", "bef9a3f7b2c67915", "c67178f2e372532b",
  "ca273eceea26619c", "d186b8c721c0c207", "eada7dd6cde0eb1e", "f57d4f7fee6ed178", "06f067aa72176fba", "0a637dc5a2c898a6", "113f9804bef90dae", "1b710b35131c471b",
  "28db77f523047d84", "32caab7b40c72493", "3c9ebe0a15c9bebc", "431d67c49c100d4c", "4cc5d4becb3e42b6", "597f299cfc657e2a", "5fcb6fab3ad6faec", "6c44198c4a475817"].map((h) => BigInt("0x" + h));

function sha384(data) {
  data = Buffer.from(data);
  const M64 = (1n << 64n) - 1n;
  const rotr = (x, n) => ((x >> BigInt(n)) | (x << BigInt(64 - n))) & M64;
  const len = data.length, total = Math.ceil((len + 17) / 128) * 128;
  const m = Buffer.alloc(total);
  data.copy(m);
  m[len] = 0x80;
  m.writeBigUInt64BE(BigInt(len) * 8n, total - 8);
  const H = ["cbbb9d5dc1059ed8", "629a292a367cd507", "9159015a3070dd17", "152fecd8f70e5939", "67332667ffc00b31", "8eb44a8768581511", "db0c2e0d64f98fa7", "47b5481dbefa4fa4"].map((h) => BigInt("0x" + h));
  const W = new Array(80);
  for (let off = 0; off < total; off += 128) {
    for (let i = 0; i < 16; i++) W[i] = m.readBigUInt64BE(off + i * 8);
    for (let i = 16; i < 80; i++) {
      const s0 = rotr(W[i - 15], 1) ^ rotr(W[i - 15], 8) ^ (W[i - 15] >> 7n);
      const s1 = rotr(W[i - 2], 19) ^ rotr(W[i - 2], 61) ^ (W[i - 2] >> 6n);
      W[i] = (W[i - 16] + s0 + W[i - 7] + s1) & M64;
    }
    let [a, b, c, d, e, f, g, h] = H;
    for (let i = 0; i < 80; i++) {
      const S1 = rotr(e, 14) ^ rotr(e, 18) ^ rotr(e, 41);
      const ch = (e & f) ^ (~e & M64 & g);
      const t1 = (h + S1 + ch + K512[i] + W[i]) & M64;
      const S0 = rotr(a, 28) ^ rotr(a, 34) ^ rotr(a, 39);
      const maj = (a & b) ^ (a & c) ^ (b & c);
      const t2 = (S0 + maj) & M64;
      h = g; g = f; f = e; e = (d + t1) & M64; d = c; c = b; b = a; a = (t1 + t2) & M64;
    }
    H[0] = (H[0] + a) & M64; H[1] = (H[1] + b) & M64; H[2] = (H[2] + c) & M64; H[3] = (H[3] + d) & M64;
    H[4] = (H[4] + e) & M64; H[5] = (H[5] + f) & M64; H[6] = (H[6] + g) & M64; H[7] = (H[7] + h) & M64;
  }
  const out = Buffer.alloc(48);
  for (let i = 0; i < 6; i++) out.writeBigUInt64BE(H[i], i * 8);
  return out;
}

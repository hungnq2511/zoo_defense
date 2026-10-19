using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;
using ZooDefense.Game;

namespace ZooDefense.Online
{
    /// <summary>
    /// Calls the Cloud Code script "validate_purchase" (CloudCode/validate_purchase.js) over the Cloud Code REST API,
    /// signed in as the player. No extra package needed.
    /// </summary>
    public sealed class UgsReceiptValidator : IReceiptValidator
    {
        const string Script = "validate_purchase";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() { ReceiptValidation.Service = new UgsReceiptValidator(); }

        [Serializable] class Body { public Params @params = new Params(); }
        [Serializable] class Params { public string store, productId, transactionId, jws, receipt; }
        [Serializable] class Output { public Result output; }
        [Serializable] class Result { public bool valid; public string reason; public string environment; }

        public async void Validate(string store, string productId, string transactionId, string jws, string receipt, Action<ReceiptVerdict, string> done)
        {
            ReceiptVerdict verdict;
            string reason;
            try
            {
                await UgsCore.EnsureSignedInAsync();
                var body = new Body();
                body.@params.store = store;
                body.@params.productId = productId;
                body.@params.transactionId = transactionId;
                body.@params.jws = jws ?? "";
                body.@params.receipt = receipt ?? "";
                var url = "https://cloud-code.services.api.unity.com/v1/projects/" + Application.cloudProjectId + "/scripts/" + Script;
                using (var req = new UnityWebRequest(url, "POST"))
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                    req.downloadHandler = new DownloadHandlerBuffer();
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
                    req.timeout = 20;
                    var op = req.SendWebRequest();
                    while (!op.isDone) await Task.Yield();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        verdict = ReceiptVerdict.Unavailable;
                        reason = "http " + req.responseCode + " " + req.error;
                    }
                    else
                    {
                        var res = JsonUtility.FromJson<Output>(req.downloadHandler.text);
                        if (res == null || res.output == null) { verdict = ReceiptVerdict.Unavailable; reason = "bad response"; }
                        else { verdict = res.output.valid ? ReceiptVerdict.Valid : ReceiptVerdict.Invalid; reason = res.output.reason; }
                    }
                }
            }
            catch (Exception e)
            {
                verdict = ReceiptVerdict.Unavailable;
                reason = e.Message;
            }
            Debug.Log("[Store] server check " + productId + " " + transactionId + ": " + verdict + " (" + reason + ")");
            if (done != null) done(verdict, reason);
        }
    }
}

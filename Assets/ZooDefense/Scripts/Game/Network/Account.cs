using System;
using UnityEngine;

namespace ZooDefense.Game
{
    public enum AccountResult
    {
        Ok,
        Cancelled,
        /// <summary>That Unity account already belongs to another game profile (another phone): offer to load it here.</summary>
        AlreadyLinkedElsewhere,
        /// <summary>Unity Player Accounts is not set up in Project Settings (no client id).</summary>
        NotConfigured,
        Offline,
        Error,
    }

    /// <summary>
    /// Player account: the game always signs in as a guest; linking a Unity account (email / Google / Apple sign-in)
    /// lets the player keep their progress and get it back on another phone. Progress is backed up to the cloud.
    /// Implemented by the ZooDefense.Online assembly (Unity Gaming Services); null in an offline build.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>Signed in to the game's online services (as guest or linked).</summary>
        bool Ready { get; }
        bool Busy { get; }
        /// <summary>Waiting for the player on the account sign-in page.</summary>
        bool SigningIn { get; }
        bool IsLinked { get; }
        /// <summary>E-mail (or "Unity ID") of the linked account, null for a guest.</summary>
        string LinkedLabel { get; }
        string PlayerId { get; }
        /// <summary>Last successful cloud backup (unix seconds), 0 = never.</summary>
        long LastBackup { get; }

        /// <summary>Signs in (guest if new) and reads the link status.</summary>
        void Refresh(Action<AccountResult> done);
        /// <summary>Opens the account sign-in page, then links it to this guest profile.</summary>
        void Link(Action<AccountResult> done);
        /// <summary>Signs in with an existing linked account (e.g. from another phone) and returns its cloud save (JSON, null if none).</summary>
        void SignInExisting(Action<AccountResult, string> done);
        void Unlink(Action<AccountResult> done);
        /// <summary>Deletes the online account and its cloud data (App Store requirement). The local profile is reset by the caller.</summary>
        void DeleteAccount(Action<AccountResult> done);
        /// <summary>Uploads the profile now.</summary>
        void BackupNow(Action<AccountResult> done);
        /// <summary>The profile changed: back it up soon (batched).</summary>
        void MarkDirty();
        /// <summary>Stop waiting for the sign-in page (the player closed it).</summary>
        void CancelSignIn();
        void Update(float dt);
    }

    public static class Accounts
    {
        /// <summary>Set by the ZooDefense.Online assembly; null = offline build.</summary>
        public static IAccountService Service;

        /// <summary>Cloud save payload: the whole local profile.</summary>
        public static string Snapshot() { return GameApp.Instance != null ? JsonUtility.ToJson(GameApp.Instance.Profile) : null; }
    }
}

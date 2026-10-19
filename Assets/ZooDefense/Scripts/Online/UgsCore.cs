using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace ZooDefense.Online
{
    /// <summary>One shared Unity Services start-up + sign-in for online play and the account (never run twice at once).</summary>
    public static class UgsCore
    {
        static Task _starting;

        public static bool SignedIn
        {
            get { return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn; }
        }

        public static Task EnsureSignedInAsync()
        {
            if (SignedIn) return Task.CompletedTask;
            if (_starting == null || _starting.IsFaulted || _starting.IsCanceled || (_starting.IsCompleted && !SignedIn)) _starting = StartAsync();
            return _starting;
        }

        static async Task StartAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                var init = new InitializationOptions();
#if UNITY_EDITOR
                // Editor clones (Multiplayer Play Mode / ParrelSync) need different player ids to be paired together.
                init.SetProfile("ed" + ((uint)Application.dataPath.GetHashCode() % 100000000u));
#endif
                await UnityServices.InitializeAsync(init);
            }
            // Re-uses the saved session: a linked player stays the same player id on this phone.
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log("[Online] signed in as " + AuthenticationService.Instance.PlayerId);
        }
    }
}

#if ZOO_NOTIFY
using System;
using System.Collections;
using Unity.Notifications;
using UnityEngine;
using ZooDefense.Game;

namespace ZooDefense.Notifications
{
    /// <summary>
    /// Local notifications (Mobile Notifications package, unified API) for the reminders planned by Reminders /
    /// NotificationPlanner. Permission is asked once, after a good moment in the game, never at launch.
    /// </summary>
    public sealed class UnityLocalNotifications : INotificationService
    {
        const string ChannelId = "zoo_reminders";
        static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            Reminders.Service = new UnityLocalNotifications();
#endif
        }

        static void Init()
        {
            if (_initialized) return;
            _initialized = true;
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = ChannelId;
            args.AndroidChannelName = "Zoo Defense";
            args.AndroidChannelDescription = "Chests ready, daily rewards";
            args.PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Badge | NotificationPresentation.Sound;
            NotificationCenter.Initialize(args);
        }

        public bool Supported { get { return true; } }

        public void RequestPermission(Action<bool> done)
        {
            try
            {
                Init();
                Runner.Instance.StartCoroutine(Ask(done));
            }
            catch (Exception e) { Debug.LogWarning("[Notify] " + e.Message); if (done != null) done(false); }
        }

        static IEnumerator Ask(Action<bool> done)
        {
            var request = NotificationCenter.RequestPermission();
            yield return request;
            if (done != null) done(request.Status == NotificationsPermissionStatus.Granted);
        }

        public void Schedule(int id, string title, string body, DateTime fireTimeLocal)
        {
            try
            {
                Init();
                var n = new Notification { Identifier = id, Title = title, Text = body, Badge = 1 };
                NotificationCenter.ScheduleNotification(n, new NotificationDateTimeSchedule(fireTimeLocal));
            }
            catch (Exception e) { Debug.LogWarning("[Notify] schedule: " + e.Message); }
        }

        public void CancelAll()
        {
            try
            {
                Init();
                NotificationCenter.CancelAllScheduledNotifications();
                NotificationCenter.CancelAllDeliveredNotifications();
                NotificationCenter.ClearBadge();
            }
            catch (Exception e) { Debug.LogWarning("[Notify] cancel: " + e.Message); }
        }

        /// <summary>Hidden object to run the permission coroutine.</summary>
        sealed class Runner : MonoBehaviour
        {
            static Runner _instance;
            public static Runner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("ZooNotifications");
                        go.hideFlags = HideFlags.HideAndDontSave;
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<Runner>();
                    }
                    return _instance;
                }
            }
        }
    }
}
#endif

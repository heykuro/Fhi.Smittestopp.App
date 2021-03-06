﻿using Android.App;
using Android.Content;
using Android.OS;
using Android.Support.V4.App;
using AndroidX.LocalBroadcastManager.Content;
using Java.Lang;
using NDB.Covid19.Droid.Views;
using NDB.Covid19.Droid.Views.Messages;
using NDB.Covid19.Enums;
using NDB.Covid19.Interfaces;
using NDB.Covid19.Utils;
using NDB.Covid19.ViewModels;
using XamarinShortcutBadger;
using static Plugin.CurrentActivity.CrossCurrentActivity;
using TaskStackBuilder = Android.Support.V4.App.TaskStackBuilder;

namespace NDB.Covid19.Droid.Utils
{
    public class LocalNotificationsManager : ILocalNotificationsManager
    {
        public const string BroadcastActionName = "no.fhi.smittestopp_exposure_notification.background_notification";
        private readonly Context _context;
        private readonly string _exposureChannelId = "exposure_channel";
        private readonly string _backgroundFetchChannelId = "background_channel";
        private readonly string _permissionsChannelId = "permissions_channel";

        private Context NotificationContext => _context ?? Current.Activity ?? Current.AppContext;

        public LocalNotificationsManager(Context context = null)
        {
            _context = context;
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            {
                // Notification channels are new in API 26 (and not a part of the
                // support library). There is no need to create a notification
                // channel on older versions of Android.
                return;
            }

            CreateChannels();
        }

        private void CreateChannels()
        {
            NotificationChannel exposureChannel = new NotificationChannel(
                _exposureChannelId,
                NotificationChannelsViewModel.NOTIFICATION_CHANNEL_EXPOSURE_NAME,
                NotificationImportance.High
            )
            {
                Description = NotificationChannelsViewModel.NOTIFICATION_CHANNEL_EXPOSURE_DESCRIPTION,
            };

            exposureChannel.SetShowBadge(true);

            NotificationChannel backgroundFetchChannel = new NotificationChannel(
                _backgroundFetchChannelId,
                NotificationChannelsViewModel.NOTIFICATION_CHANNEL_BACKGROUND_FETCH_NAME,
                NotificationImportance.Low
            )
            {
                Description = NotificationChannelsViewModel.NOTIFICATION_CHANNEL_BACKGROUND_FETCH_DESCRIPTION,
            };

            backgroundFetchChannel.SetShowBadge(false);

            NotificationChannel permissionsChannel = new NotificationChannel(
                _permissionsChannelId,
                NotificationChannelsViewModel.NOTIFICATION_CHANNEL_PERMISSIONS_NAME,
                NotificationImportance.High
            )
            {
                Description = NotificationChannelsViewModel.NOTIFICATION_CHANNEL_PERMISSIONS_DESCRIPTION,
            };

            permissionsChannel.SetShowBadge(true);

            NotificationManager notificationManager =
                (NotificationManager) NotificationContext.GetSystemService(Context.NotificationService);
            notificationManager?.CreateNotificationChannel(exposureChannel);
            notificationManager?.CreateNotificationChannel(backgroundFetchChannel);
            notificationManager?.CreateNotificationChannel(permissionsChannel);
        }

        public void GenerateLocalNotification(NotificationViewModel notificationViewModel, int triggerInSeconds)
        {
            BroadcastNotification(notificationViewModel, NotificationType.Local);
        }
        
        public Notification CreateNotification(NotificationViewModel notificationViewModel)
        {
            string channelId;
            int notificationPriority;
            switch (notificationViewModel.Type)
            {
                case NotificationsEnum.NewMessageReceived:
                    channelId = _exposureChannelId;
                    notificationPriority = NotificationCompat.PriorityHigh;
                    break;
                case NotificationsEnum.BackgroundFetch:
                    channelId = _backgroundFetchChannelId;
                    notificationPriority = NotificationCompat.PriorityLow;
                    break;
                default:
                    channelId = _permissionsChannelId;
                    notificationPriority = NotificationCompat.PriorityHigh;
                    break;
            }

            PendingIntent resultPendingIntent = InitResultIntentBasingOnViewModel(notificationViewModel);
            NotificationCompat.Builder builder = new NotificationCompat.Builder(NotificationContext, channelId)
                .SetAutoCancel(true) // Dismiss the notification from the notification area when the user clicks on it
                .SetContentTitle(notificationViewModel.Title) // Set the title
                .SetContentText(notificationViewModel.Body) // the message to display.
                .SetContentIntent(resultPendingIntent) // Start up this activity when the user clicks the intent.
                .SetVibrate(null)
                .SetSound(null)
                .SetNumber(1)
                .SetCategory(NotificationCompat.CategoryMessage)
                .SetOnlyAlertOnce(true)
                .SetPriority(notificationPriority);

            // This is the icon to display
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            {
                builder.SetColor(Resource.Color.colorPrimary);
            }

            builder.SetSmallIcon(Resource.Drawable.ic_notification);

            Notification notification = builder.Build();

            bool isLowerVersion = Build.VERSION.SdkInt < BuildVersionCodes.O;
            bool isBadgeCounterSupported = ShortcutBadger.IsBadgeCounterSupported(NotificationContext);
            bool isMessage = notificationViewModel.Type == NotificationsEnum.NewMessageReceived;
            bool areNotificationsEnabled = NotificationManagerCompat.From(NotificationContext).AreNotificationsEnabled();

            // Use Plugin for badges on older platforms that support them
            if (isLowerVersion &&
                isBadgeCounterSupported &&
                isMessage &&
                areNotificationsEnabled)
            {
                ShortcutBadger.ApplyNotification(NotificationContext, notification, 1);
            }

            return notification;
        }

        public void GenerateLocalNotificationOnlyIfInBackground(NotificationViewModel viewModel)
        {
            BroadcastNotification(viewModel, NotificationType.InBackground);
        }

        private PendingIntent InitResultIntentBasingOnViewModel(NotificationViewModel notificationViewModel)
        {
            // When the user clicks the notification, SecondActivity will start up.
            Intent resultIntent;

            // Construct a back stack for cross-task navigation:
            TaskStackBuilder stackBuilder = TaskStackBuilder.Create(NotificationContext);

            if (OnboardingStatusHelper.Status == OnboardingStatus.CountriesOnboardingCompleted &&
                notificationViewModel.Type == NotificationsEnum.NewMessageReceived.Data().Type)
            {
                resultIntent = new Intent(NotificationContext, typeof(MessagesActivity));
                stackBuilder.AddParentStack(Class.FromType(typeof(MessagesActivity)));
            }
            else
            {
                resultIntent = new Intent(NotificationContext, typeof(InitializerActivity));
                stackBuilder.AddParentStack(Class.FromType(typeof(InitializerActivity)));
            }

            stackBuilder.AddNextIntent(resultIntent);

            // Create the PendingIntent with the back stack:
            return stackBuilder.GetPendingIntent(0, (int) PendingIntentFlags.UpdateCurrent);
        }

        public void GenerateLocalPermissionsNotification(NotificationViewModel viewModel)
        {
            BroadcastNotification(viewModel, NotificationType.Permissions);
        }

        private static void BroadcastNotification(NotificationViewModel viewModel, NotificationType type)
        {
            Intent intent = new Intent();
            intent.SetAction(BroadcastActionName);
            intent.PutExtra("type", (int) type);
            intent.PutExtra("data", (int) viewModel.Type);
            LocalBroadcastManager
                .GetInstance(Current.Activity ?? Current.AppContext)
                .SendBroadcast(intent);
        }
    }
}
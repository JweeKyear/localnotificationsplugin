using Android.App;
using Android.Content;
using Android.Support.V4.App;
using Plugin.LocalNotifications.Abstractions;
using System;
using System.IO;
using System.Xml.Serialization;
using Android.OS;
using Android.Widget;
using Android.Graphics;
using Java.Net;
using System.Threading.Tasks;

namespace Plugin.LocalNotifications
{
    /// <summary>
    /// Local Notifications implementation for Android
    /// </summary>
    public class LocalNotificationsImplementation : ILocalNotifications
    {
        string _packageName => Application.Context.PackageName;
        NotificationManager _manager => (NotificationManager)Application.Context.ApplicationContext.GetSystemService(Context.NotificationService);

        private const string actionName = "com.beside.dotoribox.ClickAction";

        /// <summary>
        /// Get or Set Resource Icon to display
        /// </summary>
        public static int NotificationIconId { get; set; }
        public static int NotificationLargeIconId { get; set; }

        /// <summary>
        /// Show a local notification
        /// </summary>
        /// <param name="title">Title of the notification</param>
        /// <param name="body">Body or description of the notification</param>
        /// <param name="id">Id of the notification</param>
        public async void Show(string title, string body, int id = 0, int repeat = 0, string[] notifyData = null)
        {
            var activeContext = Application.Context.ApplicationContext;

            var builder = new Notification.Builder(Application.Context);
            builder.SetContentTitle(title);
            builder.SetContentText(body);

            //actionIntent.SetFlags(ActivityFlags.SingleTop);

            var url = new URL(notifyData[4]);
            //Stream stream = url.OpenStream();

            Bitmap bitmap = await Task.Run(async () =>
            {
                var connection = url.OpenConnection();
                var stream = connection.InputStream;
                Bitmap bitMap = await BitmapFactory.DecodeStreamAsync(stream);
                return bitMap;
            });

            //Bitmap bitmap = await GetBitmapAsync(url);

            if (NotificationIconId != 0)
            {
                builder.SetSmallIcon(NotificationIconId);
                builder.SetLargeIcon(bitmap);
            }
            else
            {
                builder.SetSmallIcon(Resource.Drawable.plugin_lc_smallicon);
            }

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                var channelId = $"{_packageName}.general";
                var channel = new NotificationChannel(channelId, "General", NotificationImportance.Default);

                _manager.CreateNotificationChannel(channel);

                builder.SetChannelId(channelId);
            }
            else
            {
                //activeContext.StartService(actionIntent);
            }
            Intent actionIntent = new Intent();
            actionIntent.SetAction(actionName);
            actionIntent.PutExtra("id", id);
            actionIntent.PutExtra("actionType", "ShowWeb");
            actionIntent.PutExtra("notifyData", notifyData);
            actionIntent.SetType("text/plain");
            PendingIntent actionPendingIntent = PendingIntent.GetService(activeContext, id, actionIntent, PendingIntentFlags.OneShot);

            //var stackBuilder = Android.Support.V4.App.TaskStackBuilder.Create(Application.Context);
            //stackBuilder.AddNextIntent(actionIntent);
            //var actionPendingIntent = stackBuilder.GetPendingIntent(0, (int)PendingIntentFlags.OneShot);
            builder.SetContentIntent(actionPendingIntent);

            Intent settingIntent = new Intent();
            settingIntent.SetAction(actionName);
            settingIntent.PutExtra("id", id);
            settingIntent.PutExtra("actionType", "Edit");
            settingIntent.PutExtra("notifyData", notifyData);
            settingIntent.SetType("text/plain");//이거 말고 다른 type 찾아보기
            PendingIntent settingPendingIntent = PendingIntent.GetService(activeContext, id + 1, settingIntent, PendingIntentFlags.OneShot);

            Intent deleteIntent = new Intent();
            deleteIntent.SetAction(actionName);
            deleteIntent.PutExtra("id", id);
            deleteIntent.PutExtra("actionType", "Delete");
            deleteIntent.PutExtra("notifyData", notifyData);
            deleteIntent.SetType("text/plain");//이거 말고 다른 type 찾아보기
            PendingIntent deletePendingIntent = PendingIntent.GetService(activeContext, id + 2, deleteIntent, PendingIntentFlags.OneShot);

            builder.AddAction(new Notification.Action(NotificationIconId, "삭제", deletePendingIntent));
            builder.AddAction(new Notification.Action(NotificationIconId, "설정", settingPendingIntent));
            builder.SetAutoCancel(true);

            _manager.Notify(id, builder.Build());

            if (repeat != 0)
            {
                var dateTime = DateTime.Now;
                switch (repeat)
                {
                    case 1:
                        dateTime = dateTime.AddDays(1);
                        break;
                    case 2:
                        dateTime = dateTime.AddDays(7);
                        break;
                    case 3:
                        dateTime = dateTime.AddMonths(1);
                        break;
                    case 4:
                        dateTime = dateTime.AddYears(1);
                        break;
                    case 5:
                        dateTime = dateTime.AddSeconds(20);
                        break;
                }
                CrossLocalNotifications.Current.Show(title, body, id, dateTime, repeat, notifyData);
            }
        }

        public static Intent GetLauncherActivity()
        {
            var packageName = Application.Context.PackageName;
            return Application.Context.PackageManager.GetLaunchIntentForPackage(packageName);
        }

        private async Task<Bitmap> GetBitmapAsync(URL url)
        {
            Stream stream = url.OpenStream();
            Bitmap bitmap = await BitmapFactory.DecodeStreamAsync(stream);
            return bitmap;
        }

        /// <summary>
        /// Show a local notification at a specified time
        /// </summary>
        /// <param name="title">Title of the notification</param>
        /// <param name="body">Body or description of the notification</param>
        /// <param name="id">Id of the notification</param>
        /// <param name="notifyTime">Time to show notification</param>
        public void Show(string title, string body, int id, DateTime notifyTime, int repeat, string[] notifyData)
        {
            var intent = CreateIntent(id);

            var localNotification = new LocalNotification
            {
                Title = title,
                Body = body,
                Id = id,
                NotifyTime = notifyTime,
                Repeat = repeat,
                NotifyData = notifyData
            };
            if (NotificationIconId != 0)
            {
                localNotification.IconId = NotificationIconId;
            }
            else
            {
                localNotification.IconId = Resource.Drawable.plugin_lc_smallicon;
            }

            var serializedNotification = SerializeNotification(localNotification);
            intent.PutExtra(ScheduledAlarmHandler.LocalNotificationKey, serializedNotification);

            var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.CancelCurrent);
            var triggerTime = NotifyTimeInMilliseconds(localNotification.NotifyTime);
            var alarmManager = GetAlarmManager();

            alarmManager.Set(AlarmType.RtcWakeup, triggerTime, pendingIntent);
        }

        /// <summary>
        /// Cancel a local notification
        /// </summary>
        /// <param name="id">Id of the notification to cancel</param>
        public void Cancel(int id)
        {
            var intent = CreateIntent(id);
            var pendingIntent = PendingIntent.GetBroadcast(Application.Context, 0, intent, PendingIntentFlags.CancelCurrent);

            var alarmManager = GetAlarmManager();
            alarmManager.Cancel(pendingIntent);

            var notificationManager = NotificationManagerCompat.From(Application.Context);
            notificationManager.Cancel(id);
        }

        private Intent CreateIntent(int id)
        {
            return new Intent(Application.Context, typeof(ScheduledAlarmHandler))
                .SetAction("LocalNotifierIntent" + id);
        }


        private AlarmManager GetAlarmManager()
        {
            var alarmManager = Application.Context.GetSystemService(Context.AlarmService) as AlarmManager;
            return alarmManager;
        }

        private string SerializeNotification(LocalNotification notification)
        {
            var xmlSerializer = new XmlSerializer(notification.GetType());
            using (var stringWriter = new StringWriter())
            {
                xmlSerializer.Serialize(stringWriter, notification);
                return stringWriter.ToString();
            }
        }

        private long NotifyTimeInMilliseconds(DateTime notifyTime)
        {
            var utcTime = TimeZoneInfo.ConvertTimeToUtc(notifyTime);
            var epochDifference = (new DateTime(1970, 1, 1) - DateTime.MinValue).TotalSeconds;

            var utcAlarmTimeInMillis = utcTime.AddSeconds(-epochDifference).Ticks / 10000;
            return utcAlarmTimeInMillis;
        }
    }
}
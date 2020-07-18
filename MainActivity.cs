using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Runtime;
using Android.Widget;
using System.Threading;
using Android.Media;
using System;
using System.Runtime.Remoting.Messaging;
using Android.Views;
using Android.Graphics.Drawables;

namespace Timer
{
    [Activity(Label = "@string/app_name", Theme = "@android:style/Theme.Black.NoTitleBar", MainLauncher = true)]
    public class MainActivity : Activity
    {
        private Button addHour, addMinute, addSecond, addMillsecond, minusHour, minusMinute, minusSecond, minusMillsecond;
        private TextView hour, minute, second, millsecond;
        private Button start, clear;
        private long millSeconds = 0;
        private Thread timeWalk;
        private bool running = false;
        private long currentMillseconds = 0;
        private System.Timers.Timer timer;
        private long pauseBegin = -1, pauseTicks = 0;
        private object milSecLock = new object();
        private bool needRefresh = false;
        private object refreshLock = new object();
        private Handler refreshTextHandler = new Handler(Looper.MainLooper);
        private bool braking = false;
        Ringtone r /* = RingtoneManager.GetRingtone(ApplicationContext, RingtoneManager.GetDefaultUri(RingtoneType.Alarm))*/;

        private void SetControlsRef()
        {
            // set controls reference.
            addHour = FindViewById<Button>(Resource.Id.ButtonAddHour);
            addMinute = FindViewById<Button>(Resource.Id.ButtonAddMinute);
            addSecond = FindViewById<Button>(Resource.Id.ButtonAddSecond);
            addMillsecond = FindViewById<Button>(Resource.Id.ButtonAddMillSecond);

            minusHour = FindViewById<Button>(Resource.Id.ButtonMinusHour);
            minusMinute = FindViewById<Button>(Resource.Id.ButtonMinusMinute);
            minusSecond = FindViewById<Button>(Resource.Id.ButtonMinusSecond);
            minusMillsecond = FindViewById<Button>(Resource.Id.ButtonMinusMillSecond);

            //hour = FindViewById<TextView>(Resource.Id.B)
            hour = FindViewById<TextView>(Resource.Id.TextViewHour);
            minute = FindViewById<TextView>(Resource.Id.TextViewMinute);
            second = FindViewById<TextView>(Resource.Id.TextViewSecond);
            millsecond = FindViewById<TextView>(Resource.Id.TextViewMillsSecond);

            start = FindViewById<Button>(Resource.Id.ButtonStart);
            clear = FindViewById<Button>(Resource.Id.ButtonClear);
        }

        private void SetAddMinusButtonEnable(bool e)
        {
            addHour.Enabled = e;
            addMinute.Enabled = e;
            addSecond.Enabled = e;
            addMillsecond.Enabled = e;

            minusHour.Enabled = e;
            minusMinute.Enabled = e;
            minusSecond.Enabled = e;
            minusMillsecond.Enabled = e;
        }

        private void RefreshTextView()
        {
            lock (refreshLock)
            {
                if (!needRefresh)
                    return;
                needRefresh = false;
                lock (refreshLock)
                {
                    if (millSeconds < 0)
                        millSeconds = 0;
                    currentMillseconds = millSeconds;
                    millsecond.Text = (millSeconds % 1000).ToString() + "\n毫秒";
                    second.Text = (millSeconds / 1000 % 60).ToString() + "\n秒";
                    minute.Text = (millSeconds / 1000 / 60 % 60).ToString() + "\n分";
                    hour.Text = (millSeconds / 1000 / 60 / 60).ToString() + "\n时";
                }
            }
        }

        private void TimeWalk()
        {
            long need = millSeconds;
            long startTime = System.DateTime.UtcNow.Ticks;
            while (millSeconds > 0)
                lock (milSecLock)
                {
                    millSeconds = need - (System.DateTime.UtcNow.Ticks - startTime - pauseTicks) / 10000;
                    lock (refreshLock) needRefresh = true;
                }

            running = false;
            pauseTicks = 0;

            RunOnUiThread(() =>
            {
                start.Background = Resources.GetDrawable(Resource.Drawable.StartButton);
                clear.Background = Resources.GetDrawable(Resource.Drawable.StopButton);

                //SetAddMinusButtonEnable(true);

                start.Enabled = false;
            });

            r.Play();
        }

        
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            RequestWindowFeature(Android.Views.WindowFeatures.NoTitle);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            SetControlsRef();

            // set events
            addHour.Click += (sender, e) => { millSeconds += 60 * 60 * 1000; lock (refreshLock) needRefresh = true; };
            addMinute.Click += (sender, e) => { millSeconds += 60 * 1000; lock (refreshLock) needRefresh = true; };
            addSecond.Click += (sender, e) => { millSeconds += 1000; lock (refreshLock) needRefresh = true; };
            addMillsecond.Click += (sender, e) => { millSeconds += 1; lock (refreshLock) needRefresh = true; };
            addMillsecond.LongClick += (sender, e) => { millSeconds += 50; lock (refreshLock) needRefresh = true; };

            minusHour.Click += (sender, e) => { millSeconds -= 60 * 60 * 1000; lock (refreshLock) needRefresh = true; };
            minusMinute.Click += (sender, e) => { millSeconds -= 60 * 1000; lock (refreshLock) needRefresh = true; };
            minusSecond.Click += (sender, e) => { millSeconds -= 1000; lock (refreshLock) needRefresh = true; };
            minusMillsecond.Click += (sender, e) => { millSeconds -= 1; lock (refreshLock) needRefresh = true; };
            minusMillsecond.LongClick += (sender, e) => { millSeconds -= 50; lock (refreshLock) needRefresh = true; };

            //addHour.Touch += (sender, e) => SwitchPressAdd((Button)sender, e);
            //addMinute.Touch += (sender, e) => SwitchPressAdd((Button)sender, e);
            //addSecond.Touch += (sender, e) => SwitchPressAdd((Button)sender, e);
            //addMillsecond.Touch += (sender, e) => SwitchPressAdd((Button)sender, e);

            start.Click += (sender, e) => 
            { 
                if (running)
                {
                    timeWalk.Suspend();
                    start.Background = Resources.GetDrawable(Resource.Drawable.StartButton);
                    pauseBegin = System.DateTime.UtcNow.Ticks;                    
                    braking = true;
                }
                else
                {
                    r = RingtoneManager.GetRingtone(ApplicationContext, RingtoneManager.GetDefaultUri(RingtoneType.Ringtone));
                    braking = false;
                    if (timeWalk == null)
                    {
                        timeWalk = new Thread(new ThreadStart(TimeWalk));
                        pauseTicks = 0;
                        timeWalk.Start();                        
                    }
                    else
                    {
                        pauseTicks += System.DateTime.UtcNow.Ticks - pauseBegin;
                        timeWalk.Resume();
                        braking = false;
                    }
                    start.Background = Resources.GetDrawable(Resource.Drawable.PauseButton);
                    clear.Background = Resources.GetDrawable(Resource.Drawable.StopButton);

                    SetAddMinusButtonEnable(false);
                }
                running = !running;
                RefreshTextView();
            };
            
            clear.Click += (sender, e) =>
            {
                clear.Enabled = false;
                try
                {
                    if (timeWalk != null && braking)
                        timeWalk.Resume();
                }
                catch (Exception) { }

                try
                {
                    if (timeWalk != null)
                    timeWalk.Abort();
                }
                catch (Exception) { }

                SetAddMinusButtonEnable(true);

                start.Enabled = true;
                if (r != null && r.IsPlaying)
                    r.Stop();
                running = false;
                timeWalk = null;
                millSeconds = 0;
                start.Background = Resources.GetDrawable(Resource.Drawable.StartButton);
                clear.Background = Resources.GetDrawable(Resource.Drawable.ClearButton);
                milSecLock = new object();
                lock (refreshLock) needRefresh = true;
                clear.Enabled = true;
            };            

            timer = new System.Timers.Timer(10);
            timer.Elapsed += (source, e) => refreshTextHandler.Post(RefreshTextView);
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
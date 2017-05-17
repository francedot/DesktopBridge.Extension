var notifyIcon = new NotifyIcon
{
BalloonTipText = @"Hello, NotifyIcon!",
Text = @"Hello, NotifyIcon!",
Icon = new Icon(@"NotifyIcon.ico"),
Visible = true
};
notifyIcon.ShowBalloonTip(1000);
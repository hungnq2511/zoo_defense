#import <AppTrackingTransparency/AppTrackingTransparency.h>

typedef void (*ATTCallback)(int status);

extern "C"
{
    // 0 = Not Determined, 1 = Restricted, 2 = Denied, 3 = Authorized
    int _ATT_GetStatus()
    {
        if (@available(iOS 14, *))
        {
            return (int)[ATTrackingManager trackingAuthorizationStatus];
        }
        return 3;
    }

    void _ATT_RequestPermission(ATTCallback callback)
    {
        if (@available(iOS 14, *))
        {
            [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status)
            {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (callback) callback((int)status);
                });
            }];
        }
        else
        {
            if (callback) callback(3);
        }
    }
}

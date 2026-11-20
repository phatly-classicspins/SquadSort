#import <UIKit/UIKit.h>
#import <AudioToolbox/AudioToolbox.h>

// Same feel as Money Sort's FlickSortHaptics.mm: 0 weak, 1 medium, 2 strong (SqadSortHapticBackend.Strength).
extern "C" void SqadSort_PlayImpactHaptic(int strength)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (@available(iOS 10.0, *))
        {
            if (strength <= 0)
            {
                // A short selection tick is intentionally subtler than an impact.
                UISelectionFeedbackGenerator *generator =
                    [[UISelectionFeedbackGenerator alloc] init];
                [generator prepare];
                [generator selectionChanged];
#if !__has_feature(objc_arc)
                [generator release];
#endif
            }
            else if (strength == 1)
            {
                UIImpactFeedbackGenerator *generator =
                    [[UIImpactFeedbackGenerator alloc]
                        initWithStyle:UIImpactFeedbackStyleMedium];
                [generator prepare];
                [generator impactOccurred];
#if !__has_feature(objc_arc)
                [generator release];
#endif
            }
            else
            {
                // Notification feedback has a distinct multi-pulse pattern, making
                // win/lose feedback clearly different from UI taps and pole clears.
                UINotificationFeedbackGenerator *generator =
                    [[UINotificationFeedbackGenerator alloc] init];
                [generator prepare];
                [generator notificationOccurred:UINotificationFeedbackTypeWarning];
#if !__has_feature(objc_arc)
                [generator release];
#endif
            }
        }
        else
        {
            AudioServicesPlaySystemSound(kSystemSoundID_Vibrate);
        }
    });
}

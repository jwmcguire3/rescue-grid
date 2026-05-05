#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

static UIImpactFeedbackStyle RescueGridImpactStyle(int style, float intensity)
{
    if (style == 5 || intensity >= 0.80f)
    {
        return UIImpactFeedbackStyleHeavy;
    }

    if (style == 3 || style == 6 || intensity >= 0.50f)
    {
        return UIImpactFeedbackStyleMedium;
    }

    if (style == 2 || intensity >= 0.30f)
    {
        return UIImpactFeedbackStyleLight;
    }

    return UIImpactFeedbackStyleSoft;
}

static void RescueGridPlayPulse(int style, float intensity)
{
    if (@available(iOS 13.0, *))
    {
        UIImpactFeedbackGenerator *generator =
            [[UIImpactFeedbackGenerator alloc] initWithStyle:RescueGridImpactStyle(style, intensity)];
        [generator prepare];
        [generator impactOccurredWithIntensity:MAX(0.0f, MIN(1.0f, intensity))];
        return;
    }

    UIImpactFeedbackGenerator *generator =
        [[UIImpactFeedbackGenerator alloc] initWithStyle:RescueGridImpactStyle(style, intensity)];
    [generator prepare];
    [generator impactOccurred];
}

extern "C" void RescueGrid_PlayHapticPattern(
    int style,
    float intensity,
    int durationMs,
    float secondPulseIntensity,
    int secondPulseDelayMs,
    int secondPulseDurationMs)
{
    (void)durationMs;
    (void)secondPulseDurationMs;

    dispatch_async(dispatch_get_main_queue(), ^{
        RescueGridPlayPulse(style, intensity);

        if (secondPulseIntensity <= 0.0f || secondPulseDelayMs <= 0)
        {
            return;
        }

        dispatch_time_t delay = dispatch_time(
            DISPATCH_TIME_NOW,
            (int64_t)secondPulseDelayMs * NSEC_PER_MSEC);
        dispatch_after(delay, dispatch_get_main_queue(), ^{
            RescueGridPlayPulse(style, secondPulseIntensity);
        });
    });
}

// Observes hardware keyboard arrow keys on iOS via the GameController framework.
//
// iOS never routes hardware-keyboard key events into Unity: while a text session is
// active, input reaches the app only through the native keyboard session (text), and
// neither UI Toolkit nor Unity's Input System ever sees key events. GCKeyboard (iOS 14+,
// project minimum is 15.0) delivers key state app-wide, in parallel with text input and
// regardless of the first responder — which makes it the only managed-adjacent way to
// drive the mention suggestion list from an iPad keyboard cover.
//
// Observe-only by design: GCKeyboard cannot consume keys, so this never disturbs the
// text input itself. The handler (main queue) sets pressed flags; the C# popover poll
// (MentionHardwareKeys.cs) consumes them once per frame. No callbacks into managed code.

#import <Foundation/Foundation.h>
#import <GameController/GameController.h>

static volatile int g_UpPressed = 0;
static volatile int g_DownPressed = 0;
static bool g_Started = false;
static GCKeyboardValueChangedHandler g_InstalledHandler = nil;

static void MentionKeys_Attach(GCKeyboard *keyboard)
{
    if (keyboard == nil || keyboard.keyboardInput == nil) return;
    GCKeyboardValueChangedHandler previous = keyboard.keyboardInput.keyChangedHandler;
    // Already attached to this input (a connect notification can re-deliver the same
    // coalesced keyboard): re-wrapping would nest the chain one level per reconnect.
    if (previous != nil && previous == g_InstalledHandler) return;
    // keyChangedHandler is a single settable property, so assigning it would CLOBBER
    // any handler another consumer (e.g. Unity's input backend) installed on the same
    // coalesced keyboard. Chain the previous handler instead of replacing it. (If the
    // other party assigns after us without chaining, ours is lost until the next
    // connect notification re-attaches — unavoidable from this side.)
    keyboard.keyboardInput.keyChangedHandler = ^(GCKeyboardInput *input, GCControllerButtonInput *key, GCKeyCode keyCode, BOOL pressed)
    {
        if (previous != nil) previous(input, key, keyCode, pressed);
        if (!pressed) return;
        if (keyCode == GCKeyCodeUpArrow)
        {
            g_UpPressed = 1;
        }
        else if (keyCode == GCKeyCodeDownArrow)
        {
            g_DownPressed = 1;
        }
    };
    // Read back the property (it copies the block) so the identity check above works.
    g_InstalledHandler = keyboard.keyboardInput.keyChangedHandler;
}

extern "C" void MentionKeys_Start()
{
    if (g_Started) return;
    g_Started = true;
    MentionKeys_Attach(GCKeyboard.coalescedKeyboard);
    // A keyboard cover attached/detached mid-session arrives as a connect notification;
    // re-attach the handler to the new coalesced keyboard.
    [[NSNotificationCenter defaultCenter] addObserverForName:GCKeyboardDidConnectNotification
                                                      object:nil
                                                       queue:[NSOperationQueue mainQueue]
                                                  usingBlock:^(NSNotification *note)
    {
        MentionKeys_Attach((GCKeyboard *)note.object);
    }];
}

extern "C" int MentionKeys_ConsumeUp()
{
    int pressed = g_UpPressed;
    g_UpPressed = 0;
    return pressed;
}

extern "C" int MentionKeys_ConsumeDown()
{
    int pressed = g_DownPressed;
    g_DownPressed = 0;
    return pressed;
}

extern "C" int MentionKeys_HasKeyboard()
{
    return GCKeyboard.coalescedKeyboard != nil ? 1 : 0;
}

# Timeline

The storyboard timeline is laid out like Figma's motion timeline.

## Layout

- **Layers on top**: animated objects are grouped under the GT layer that holds them, top layer first, as in the
  Layers panel. A layer row shows a small layer icon, its name in bold, and a summary bar over everything animated in
  it. An animation aimed at the layer itself sits directly under the layer row.
- **One group per object** inside its layer: a row with the object's name and a grey summary bar spanning all its
  animations. Objects the title doesn't hold are listed after the layers.
- **One row per animation** below it: a start and an end keyframe diamond joined by a line.
  - The keyframes take the animation type's colour, and their **shape shows the easing** at that end, as in After
    Effects: a **diamond** where the motion is linear, a **circle** where it eases, a **square** where it bounces.
    Ease In and Bounce In act at the start, Ease Out and Bounce Out at the end, Ease In Out at both.
  - An arrow beside the name shows the direction.
  - A hollow start diamond means the animation is reversed.
- **Collapse**: the chevron in the name column folds a layer or an object away.
- **Mute**: the eye on a layer or object row mutes every animation under it.

## Editing

- **Drag a diamond** to retime that end of the animation.
- **Drag the line** to move the whole animation.
- **Drag a summary bar** to move every animation of that object, or of that whole layer, together.

## Pinned ruler and names

The time ruler at the top and the name column on the left stay in place while you scroll, so you always see the
time and which object a row belongs to.

## Working area

The ruler marks in blue the time spans in which something animates, and the rows get a faint wash over the same
spans. It merges the spans of every animation that isn't muted. Gaps where nothing animates stay unmarked.

## Detaching the timeline

The **detach** button at the top right of the timeline moves it into its own window, which you can put on another
monitor.

- Keyboard shortcuts still work while that window has focus.
- Closing the window docks the timeline back under the canvas.
- The window's position and size are remembered. They're reused only while that spot is still on a connected
  screen.

## The timeline bar

The bar above the track (storyboard picker, transport, commands, clip settings and the direction pad) never scrolls.
When the panel gets narrow, its groups wrap onto more lines.

- **Drag the grip** under the bar to make it taller or shorter.
- **Double-click the grip** to fit the bar to its contents again.

## Zoom and playback keys

- **Cmd+scroll** (Ctrl+scroll on Windows and Linux) over the track zooms the time axis in and out. The moment under
  the pointer stays where it is. The zoom slider at the top right follows along and sets the same range (12–600 px
  per second).
- **Space** plays and pauses only while you're working in the timeline: when it was the last thing you clicked, when
  the pointer is over it, or when its detached window is in front. Anywhere else (the canvas, the layers panel, a text
  box) Space does nothing to playback.

## Transition slots (1 · 2 · 3)

Like GT Title Designer, each object can have up to three animations per storyboard, shown as numbered **slots** in
the timeline bar.

- The slot buttons show the object's slots on the storyboard you're viewing. A **green** number means that slot holds
  an animation. The object is the one whose clip is selected on the timeline, otherwise what's selected on the canvas
  (with several selected, every one gets the change).
- **Click a slot**, then pick its animation type from the list under it. Picking a filled slot also selects its clip
  so you can edit timing, easing and direction in the strip.
- Changing a slot's type keeps its delay, duration, reverse, easing and speed, as GT does.
- Picking slot 2 or 3 while earlier slots are empty fills the gap with GT's own `<None Object="…"/>` placeholder rows,
  and **None** empties a slot the same way. These are exactly the rows GT writes, so the file stays plain GT.
- Every change is one undo step.

# Timeline

The storyboard timeline is laid out like Figma's motion timeline.

## Layout

- **One group per object**: a row with the object's name and a grey summary bar spanning all its animations.
- **One row per animation** below it: a start and an end keyframe diamond joined by a line.
  - The diamonds take the animation type's colour.
  - An arrow beside the name shows the direction.
  - A hollow start diamond means the animation is reversed.
- **Collapse**: the chevron in the name column folds a group away.
- **Mute**: the eye on a group row mutes every animation of that object.

## Editing

- **Drag a diamond** to retime that end of the animation.
- **Drag the line** to move the whole animation.
- **Drag a group's summary bar** to move every animation of that object together.

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

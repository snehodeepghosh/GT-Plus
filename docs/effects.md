# Effects

Three GT effects that GT Plus kept in the file but didn't show are now editable on the **Effects** tab and drawn on
the canvas. They are saved as GT's own `<Effect>` entries, so vMix shows them too.

## Skew

The **Skew** group leans the object sideways (**X**) or up and down (**Y**), in degrees.

- The sliders cover −60° to 60°. The number boxes accept up to ±85°.
- **Reset** sets both back to 0, which removes the effect.
- The object leans about its centre, as in GT.
- A **positive X** leans the object forward, its top to the right, like italic. A negative X leans it back. The
  numbers mean the same as in GT's own Skew boxes.
- A skewed mask object cuts a skewed shape.

In the file: `<Effect Type="Skew" Mode="Replace" ... />`.

## Reflection

The **Reflection** group draws a mirror image of the object under its bottom edge. It starts at 50% opacity and
fades out.

- **Preset**: Short (25%), Half (50%) or Full (100%).
- **Depth**: how far down the reflection reaches before it has faded out, as a share of the object's height.
  0 removes the effect.

In the file: `<Effect Type="Reflection" Mode="Finish" Depth="0.5" />`.

## Texture Flip

The **Texture Flip** group mirrors the object's picture.

- **Vertical**: top to bottom (`FlipY`).
- **Horizontal**: left to right (`FlipX`).

Unticking a box removes the effect.

## Order

Flips and skews are applied in the order the file lists them, as GT does. Selection handles and hit-testing still
use the object's unskewed box.

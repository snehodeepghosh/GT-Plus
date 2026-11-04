# Photoshop import

Open a Photoshop file with **File › Import PSD…**, or drop a `.psd` or `.psb` file onto the canvas. It opens as a
new, unsaved title, so the first Save asks where to put the `.gtzip`. If the current title has unsaved changes,
you're asked before it is replaced.

The import follows the rules of vMix GT Title Designer's own PSD importer (its `PSDImport` helper). It reads the
file itself, with no extra libraries.

## How layers come in

| In Photoshop | In the title |
| --- | --- |
| A top-level group | Its own GT layer, named after the group and cropped to its contents, so it can be animated as one block |
| Layers outside any group | Each run of them shares one GT layer |
| A group inside a group | Flattened into its top-level group's layer, keeping its layers |
| A text layer | An editable text object: text, font, size, bold and italic, alignment, colour, underline, strikethrough, all caps, leading and paragraph boxes |
| A pixel or shape layer | A rectangle filled with the layer's picture, cut to the canvas and trimmed of empty edges |
| A hidden layer or group | Skipped |
| A locked layer | Locked |
| Layer opacity, fill opacity, group opacity | Multiplied into the object's opacity |
| A layer mask on a pixel layer | Applied to the picture |

- **Names** are cleaned up GT's way: dots are removed, at most 15 characters are kept, and duplicates get a number.
- **Point text** is placed on Photoshop's own baseline, so it lands where Photoshop drew it.
- **Rotated and slanted text**: a type layer's transform is split into what GT can draw. The rotation becomes the
  object's rotation (`<Transform Rotate>`), the slant becomes GT's Skew effect, and the scale goes into the font size.
  Tested against Photoshop with 20° rotation, faux italic, and both together.
- **Faux italic** becomes a 10° Skew effect, Photoshop's own slant, instead of the renderer's synthetic italic. See
  [Fonts](fonts.md).
- **Reference image**: Photoshop's flattened preview of the file is loaded as the [reference image](reference.md),
  so you can lay it over the import to check it.
- **Canvas size**: any size imports. The status bar warns when vMix wouldn't accept it (its sizes are 1920×1080,
  1280×720, 3840×2160, 3412×1920, 4096×2160, 7680×4320, 720×576, 768×576 and 720×480).

## Clipping masks

A Photoshop clipping mask (Option-click between two layers, or **Create Clipping Mask**) clips a layer to the
layer below it. That lower layer is the *base*, and several layers can be clipped to one base.

| Base | What the import does |
| --- | --- |
| A pixel, shape or text layer | The clipped object gets a GT mask that names the base: `<Mask Object="base" />`. It keeps clipping if you move, edit or animate the base |
| A group | A GT mask can only name one object, so the group's combined shape is cut into the clipped layer's pixels |
| A hidden base | Layers clipped to it are skipped, as Photoshop hides them too |

The status bar shows how many clipping masks came in.

### Differences from Photoshop

GT draws the base and the clipped object one on top of the other, while Photoshop combines a clipping group
before drawing it. As a result:

- **Semi-transparent base**: where the base is partly see-through, the clipped layer looks a little lighter
  than in Photoshop.
- **Text edges**: a clipped texture over text can show a faint rim of the text's own colour around the letters.
- **Text clipped to a group** imports unclipped, and a warning is logged.

## What is not imported

As with GT's own importer, these are not carried over:

- layer styles (drop shadows, strokes, glows)
- blend modes
- adjustment layers (Gradient Map, Curves and so on)
- masks on groups
- mixed styles within one text layer: the layer takes the first font and colour used in it

Fonts that aren't installed are listed in the status bar and drawn in a fallback font until you install them.

## Status bar

After an import the status bar lists the layers, images and text objects created, the clipping masks, how many
hidden layers were skipped, any missing fonts, and a warning if the canvas size isn't one vMix accepts.

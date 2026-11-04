# Reference image

**View › Reference** lays a picture of the original design over the title, so you can check the build matches it.

- **After a PSD import** the reference is Photoshop's own flattened preview of the file, loaded automatically.
  Press **Reference** to show it, or press **Cmd+Shift+R** (Ctrl+Shift+R on Windows) to turn it on and off.
- **Load image…** uses any PNG, JPEG, BMP, WebP or GIF instead. It is stretched to the canvas size.
- **Opacity**: the slider sets how strongly the reference shows (50% by default).
- **Difference**: wherever the title matches the reference the canvas goes black, so any mismatch lights up. It
  always shows at full strength.
- **✕** removes the reference. Opening or creating another title also removes it.

The reference is **editor-only**. It is never written into the `.gtzip` and never drawn into an exported image or
video.

PSD files saved without Photoshop's **Maximize Compatibility** option carry no flattened preview. For those, use
Load image… with an export of the design.

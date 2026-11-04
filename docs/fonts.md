# Fonts

## Replace fonts

**Home › Fonts › Replace fonts** swaps fonts in bulk, for example after a PSD import whose fonts aren't installed.

1. Choose the scope: every text object in the title, or only the selected ones.
2. The dialog lists each font in use (family, weight and style), with how many objects use it. Fonts that aren't
   installed are marked **not installed**.
3. For any row, pick a new family, and optionally a new weight and style. Rows you leave as they are don't change.
4. Press **Replace**. The whole change is one undo step.

Only each text object's existing `FontFamily`, `FontWeight` and `FontStyle` attributes are rewritten, so the title
stays standard GT.

## Faux italic

The slanted **I** button next to Italic in the Font group applies faux italic the way Photoshop does it: the text
slants by 10°, pivoting on its first baseline so the line stays where it is.

- It is saved as GT's own Skew effect (`<Effect Type="Skew" Angle="-10,0" />`), so vMix draws exactly the same slant.
  The renderer's synthetic italic looks different in this editor, in vMix and in Photoshop.
- The 10° angle was measured from Photoshop's own render of faux-italic text.
- Clicking it again removes the slant. You can fine-tune it on **Effects › Skew**.
- The **Italic** button is unchanged: it uses the font's real italic face where the font has one.

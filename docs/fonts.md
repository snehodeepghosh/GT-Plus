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

## Font weights

The **weight** menu lists the faces the font really has, named as the font names them, each with a sample drawn in
that face. GFF Latin, for example, shows Thin, Light, Regular, Medium, Bold, ExtraBold and Black. Fonts that aren't
installed show GT's full list of weight names.

Each face is matched by its **true weight**, the number stored in the font file (its OS/2 `usWeightClass`). That is
the number vMix matches a title's weight against on Windows. macOS reports some weights differently: it gives GFF
Latin's Black face as 947 and its ExtraBold as 867, so a plain "nearest weight" lookup for Black (900) used to draw
ExtraBold. The editor now resolves every weight the way vMix does, so it draws the same face vMix will.

The title still stores GT's usual weight names (Thin, Light, Regular, Medium, Bold, ExtraBold, Black and so on).

## Fonts in PSD imports

A PSD names each font by its exact PostScript name, for example `GFF-Latin-Black`. The importer looks that name up
among the installed faces first, so it gets the right family ("GFF Latin") and the face's true weight. Only if the
face isn't installed does it fall back to reading the family and weight out of the name.

## Faux italic

The slanted **I** button next to Italic in the Font group applies faux italic the way Photoshop does it: the text
slants forward by 10°.

- It is saved as GT's own Skew effect (`<Effect Type="Skew" Angle="10,0" />`), slanting about the text's centre, so
  vMix draws exactly the same slant.
  The renderer's synthetic italic looks different in this editor, in vMix and in Photoshop.
- The 10° angle was measured from Photoshop's own render of faux-italic text.
- Clicking it again removes the slant. You can fine-tune it on **Effects › Skew**.
- The **Italic** button is unchanged: it uses the font's real italic face where the font has one.

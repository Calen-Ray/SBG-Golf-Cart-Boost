# Changelog

## v0.1.2

- Shrink the "Press [button] to boost!" prompt and make it scale reliably across aspect
  ratios. v0.1.0/0.1.1 set `fontSize=36` on a 640×60 rect with the default `CanvasScaler`
  match-mode (width-only), which ballooned the label on ultrawide monitors. Now uses TMP
  auto-sizing capped at 22 pt on a 420×40 rect with `matchWidthOrHeight=1` (height match),
  so the prompt sits roughly the same height as the vanilla "Exit" hint underneath it
  regardless of resolution.

## v0.1.1

- Replace placeholder icon with the painted cover art.

## v0.1.0

- Initial release. Right click while driving for a 1.5 s speed boost with a 4 s cooldown.
  Reuses the coffee `SpeedBoost` status so the existing cart speed multiplier and speed-line
  VFX/audio fire automatically. UI prompt shows availability and fades during cooldown.

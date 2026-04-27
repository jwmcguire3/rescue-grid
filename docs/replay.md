# Replay Notes

Replay and smoke fingerprints include target readiness intentionally.

`TargetReadiness` is player-visible rules state, so trajectory comparisons should
fail when target rescue-readiness changes even if board tiles, dock contents, and
final outcomes still match. Golden replay or smoke expectations should be
updated intentionally after readiness-rule changes rather than preserving the old
target boolean fingerprint shape.

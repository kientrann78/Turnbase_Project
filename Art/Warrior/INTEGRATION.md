# Warrior integration

The Knight prefab now uses `Assets/Animation/Warrior/Warrior.controller` and the 24 individual transparent PNGs in `Assets/Sprites/Warrior`. Existing gameplay components, collider, stats, UI, VFX, skills and turn mechanics remain on the same prefab.

- Idle: breathing loop, 1 second.
- Attack: overhead axe chop, 0.7 seconds.
- Hit: recoil, 0.4 seconds, returns to Idle.
- Death: held recoil pose as a fallback, not a bespoke death animation.

The Attack trigger always plays the single overhead axe chop with the original 0.7-second combat timing.

`import_warrior.py` performs local background extraction, sprite slicing and Unity YAML asset generation. Original opaque draft remains under Art for reference; transparent output is `Warrior-transparent.png`. Preview GIFs are provided for the three requested animations. The single sprite imports use point filtering, no compression, 64 pixels per unit and a shared pivot to retain character placement.

Validation: C# build passes; all 24 frames contain real alpha, animation sprite GUIDs resolve, and controller state/transition references are valid. Unity Play Mode appearance and timing have not been verified. The old controller lacked a Hit parameter; the new controller explicitly defines Hit.

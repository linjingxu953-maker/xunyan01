# Character Image Assets

Put customizable mascot images under this folder. The app resolves character image folders from the project root, so these paths can be used in `character-profile.json` and in the role panel:

```text
assets/characters/default
assets/characters/yan
assets/characters/yue guang
assets/characters/feng lin yu ren
```

## Folder Rules

`assets/characters` is the shared character image root. Every role should have one dedicated subfolder:

```text
assets/characters/
  feng lin yu ren/  current default character: 枫林渔人
  yan/              legacy first character: 微风
  yue guang/        月光
  <new-role>/   images for one new role
```

Do not place role images directly in `assets/characters`. Keep images grouped by role folder so the settings center can switch, preview, import, and export a character without mixing files from different roles.

`assets/characters/default` is kept only as a compatibility fallback for older profiles. It is not shown as a separate selectable role. The legacy first character now uses `assets/characters/yan` and is named 微风.

Supported image formats: `.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`.

Recommended transparent PNG size: `512x512` or `1024x1024`.

## Expected Files

Each character folder can contain these files:

```text
avatar.png       fallback image
idle.png         Idle
listening.png    Listening
thinking.png     Understanding
reading.png      ReadingContext
planning.png     Planning
waiting.png      WaitingApproval
working.png      Working
memory.png       MemoryConfirm
reporting.png    Reporting
completed.png    Completed
error.png        Error
```

Resolution order:

1. Current state image, such as `working.png`
2. `avatar.png`
3. Text avatar from the role profile

Missing images are safe. The UI falls back automatically.

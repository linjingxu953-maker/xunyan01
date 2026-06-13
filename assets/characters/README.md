# Character Image Assets

Put customizable mascot images under this folder. The app resolves character image folders from the project root, so these paths can be used in `character-profile.json` and in the role panel:

```text
assets/characters/default
assets/characters/developer
assets/characters/operator
assets/characters/study
```

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

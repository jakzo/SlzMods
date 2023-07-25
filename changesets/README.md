To release a new version of a mod, create a Markdown file in this changesets directory and name it `GAME_MOD_BUMP.md` where `GAME` is the name of the game directory, `MOD` is the name of the mod directory and `BUMP` is one of `Patch`, `Minor` or `Major` (for example `Bonelab_SpeedrunTimer_Minor.md`). The contents of this file should be the markdown added to the mod's changelog after it is released.

These changeset files will be picked up on push to the main branch and automatically released.

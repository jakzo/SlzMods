## Decompilation

You can use the Ghidra MCP to decompile functions in the game and see the actual logic.

You should clean up and improve the decompiled code as you look at it!

If the function you are looking at has not already had these things applied, you should:

- Rename and split local variables to something meaningful
- Fix the types of literals like the common `somePointer == (Something *)0x0` which should actually be `somePointer == null`
- Rewrite into C# original source code and put it in the plate comment
- Rename functions and fields which have non-meaningful generated names
- Any other improvements to readability and correctness

Do this even if your task is about something else. Whenever you look at a function you should clean it up so that it can be better understood now and in future. Very briefly list the functions you cleaned up to the user in your response.

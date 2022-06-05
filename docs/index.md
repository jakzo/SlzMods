# 🔮 Reverse Engineering a Unity Game

I've always wondered how people mod games -- figuring out all the APIs, what they do and hooking into them to make something cool. I recently went through this process of understanding the exact logic used in a part of the game and building a mod to change it. During this I learnt how C# and Unity compile to machine code, how to read disassembled x86 code and the various tools to help with these tasks. But before I get to that, I'll explain what I'm modding and why.

- Table of contents
{:toc}

## 🎙️ Introduction

Boneworks is amazing! 😍 I bought it when I first got my VR headset a year ago but my first playthrough was a nauseating experience as I managed to get my body stuck in the monkey bars of the museum. 🤢 I never even saw an enemy before putting it down and getting into other games like Beat Saber. But about a month ago I picked it back up and to my surprise I no longer got motion sick! I played through the whole story and the side chambers and loved it so much that I decided to keep playing it and turn to one of my favourite pastimes: speedrunning. 🏃 I mostly enjoy watching speedruns and letting other people put in the hundreds of hours of grinding and glitch hunting, so two weeks in and [with a respectable time](https://www.youtube.com/watch?v=fWU1n0-W-wA) that would have placed in the top 20 on the leaderboard if I'd submitted it, this was probably the furthest I'd ever gone into running a game myself. At this point, to get better times I needed to practise tricks like [flinging from the boss claw to the finish of Streets](https://youtu.be/1nZAoV9Tna8?t=167) so I could pull them off consistently. The problem with this is that the direction the boss claw travels is totally random, so it is annoying to practise when it rarely goes the right way for a fling. 😩 That's when I set out to fix this. I decided to build a mod to force it in the right direction and also see if I could reverse engineer the logic used to decide where the boss claw will go.

## ⚙️ Technical background

This section will be pretty dense so I'll include a **tl;dr** for each section in a quote block at the beginning.

### 🏗️ How the game is compiled

> C# source code -> compiled to C# IL -> compiled to C++ code by IL2CPP -> compiled to x86 binary

Boneworks is a [Unity](https://unity.com/) game, and as such the code is written in [C#](<https://en.wikipedia.org/wiki/C_Sharp_(programming_language)>). A lot of languages (eg. [C++](https://en.wikipedia.org/wiki/C%2B%2B)) compile into instructions for a particular CPU architecture (eg. [x86](https://en.wikipedia.org/wiki/X86) which is most popular for PCs today), but C# compiles to an intermediate language (IL) which no CPU understands. Instead an execution engine (eg. [Mono](<https://en.wikipedia.org/wiki/Mono_(software)>)) interprets, compiles and runs the IL when the program is run. However this [just-in-time](https://en.wikipedia.org/wiki/Just-in-time_compilation) interpreting of the code adds overhead and for games, faster is better. This is why Unity built a tool called [IL2CPP](https://docs.unity3d.com/Manual/IL2CPP.html).

IL2CPP transforms the IL into C++ code (as the name suggests). This C++ code can then be compiled to a particular CPU architecture, eliminating the overhead of an execution engine. One thing that is important to note though: usually in C++ the names of your variables and structures don't matter because they are all compiled down to memory addresses, but C# supports (and heavily uses) [reflection](https://en.wikipedia.org/wiki/Reflective_programming) which allows code to introspect things like method names and types. IL2CPP outputs a file with all these symbols that the C++ code can use so that reflection features work. The compiled output (whether IL or IL2CPP binary) is a `.dll` file that is known in C# terminology as an _assembly_.

So now that we have some background on how the game is compiled and which data is available, we can get to the actual process of reverse engineering!

### 🪄 C# decompilers

> dnSpy and ILSpy allow reading compiled C# code but won't work for Boneworks because of IL2CPP.

[dnSpy](https://github.com/dnSpy/dnSpy) is a popular tool which can do things like showing, debugging and editing C# code for a game. But this only works for IL (because IL instructions are similar to actual C# code) and doesn't work for IL2CPP assemblies. The tool has also been archived by the owner, so is no longer under development.

[ILSpy](https://github.com/icsharpcode/ILSpy) is another tool which does the same thing as dnSpy but seems less friendly. It is older and still actively developed though.

It would be great if I could open Boneworks with these tools since it would be really easy to read the C# code and see what the boss claw logic is, but ultimately it's not possible because they only read IL, not x86 binaries produced by IL2CPP. 🚧

### 🪛 IL2CPP reversers

> Il2CppInspector outputs the classes/methods/properties, their types and locations in the assembly. Cpp2IL converts the assembly to IL (which can be used with dnSpy/ILSpy).

[Il2CppInspector](https://github.com/djkaty/Il2CppInspector) is a tool that reads the reflection metadata from a compiled IL2CPP project. This gives us a list of all the classes, methods and properties with types as well as the address where they are in the assembly. The author has a [great blog series](https://katyscode.wordpress.com/tag/il2cpp/) that goes into how it works and the various obfuscation techniques game developers use to hide the reflection metadata from reverse engineering. It worked out of the box for Boneworks though (and the developers -- Stress Level Zero -- are modding-friendly by the way) so I had no issue with this. There are plenty of output options, like creating a C# project with class/method stubs, C headers and Python scripts to assign class/method names to locations in disassemblers.

[Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) is a really interesting project that aims to reconstruct the IL from the compiled C++. C# decompilers like dnSpy/ILSpy can then be used to read the code in a much more friendly syntax than x86 assembly. Il2CppInspector development has been discontinued and recommends this project. Unfortunately it's not perfect yet and there are many instances where it cannot reconstruct something and bails out of reconstructing the entire function.

### 🤖 x86 disassemblers

> IDA is the best x86 disassembler (but automatic subroutine renaming based on reflection metadata doesn't work on free version).

There are many x86 disassemblers but two of the best are [IDA](https://hex-rays.com/ida-free/) and [Ghidra](https://ghidra-sre.org/). These read machine code (the raw 1s and 0s that CPUs understand) and converts it to assembly code (human-readable instructions, not to be confused with C# assemblies which I mentioned above). IDA is better but many useful features are only in the paid version, while Ghidra is free and open-source. Il2CppInspector supports creating scripts for automatically renaming the subroutines identified by IDA and Ghidra to their real names based on the IL reflection metadata but unfortunately IDA free cannot run scripts and there were heaps of errors in the Ghidra file. 😕 In the end I just manually renamed subroutines every time I saw a `call` instruction as I was reading the disassembled code by cross-referencing the called address with the table produced by Il2CppInspector. 😒

Assembly code is pretty easy to read but hard to understand since it is so low-level. An example of assembly instructions is this:

```asm
add rax, 100   ; adds 100 to the rax register
add rax, rbx   ; adds the value in rbx to rax
add rax, [100] ; adds the value in memory at address 100 to rax
add rax, [rbx] ; adds the value in rbx to rax
```

Basically the CPU has several registers which store a small amount of data (eg. a 64-bit integer) and reads/executes instructions from memory which operate on these registers. You can look up what instructions do and what registers are generally used for on Google and in CPU reference manuals.

I have some prior experience with x86 assembly so have no issues reading it but I'm not sure about conventions like where C++ usually stores arguments before calling another subroutine. Also there's just so much state you need to keep in your head to understand what's going on. 😵‍💫

### ⏯️ Inspecting a running game

> The incredible UnityExplorer mod lets you view and edit classes/methods/properties while the game is running.

There is an amazing mod called [UnityExplorer](https://github.com/sinai-dev/UnityExplorer) which reads the reflection metadata of the running game and allows you to view classes, edit properties, call methods and even has a C# REPL where you can run any script you want. All in real time while the game is running! 🤯 This mod was extremely helpful to quickly figure out how the boss claw works.

## 🗜️ Reverse engineering the boss claw

Alright, we're finally ready to start figuring out the internals of the boss claw! First thing is to load up the game, go to Streets and play around with UnityExplorer while watching the boss claw: watch how properties change as it moves, hook into methods to see when they're called, manually change properties and call methods to see what they do. Through this I found that its behaviour is controlled by the `BossClawAi` class (luckily to my surprise it was called the same thing in code as what the speedrunning community calls it) and I didn't spend time to learn _everything_ but here's what I was able to infer about its properties and methods:

```c#
public class BossClawAi : MonoBehaviour
{
    // Important things for boss claw RNG!
    public Vector3 _homePosition { get; set; } // the position it starts at (in the middle of the street)
    public Vector2 patrolXz { get; set; } // a plane that extends this many units from _homePosition in X and Z directions
        // When patrolling it will choose a random point on this plane to patrol to
        // The Z component is 0 (I assume an early version of the claw behaved more like an arcade machine claw which can
        // move in two dimensions but the mechanics of the overhead arms would be complicated for it to go to the drop-off
        // point so they made it held up by an overhead track instead and just reduced the Z to 0 to stay on the track)
    public UnityEngine.AI.NavMeshAgent _navAgent { get; set; } // manages pathing, movement and current target position
        // The claw is very simple so barely needs an AI but it does have to go around a corner to reach the drop-off point
    public void AiTick(); // called on each frame to perform claw logic

    // Others
    public float _aiTickFreq { get; set; } // how often AiTick() is called
    public float _lastAiTickTime { get; set; } // last time AiTick() was called
    public float patrolFrequency { get; set; } // how often to move to a random patrol point
    public float _patrolTimer { get; set; } // time when it last moved to a random patrol point
    public List<TriggerRefProxy> targetList { get; set; } // list of targets it wants to pick up
    public TriggerRefProxy _activeTarget { get; set; } // target it's currently going for
    public float _curExtension { get; set; } // percentage it's currently extended to the ground
    public float _extensionVelocity { get; set; } // speed it is extending to the ground
    public bool CaughtPrey(); // called when it has grabbed something
    public void FixedUpdate(); // called on each frame
    public void SwitchMentalState(MentalState mState);
    public void SwitchPounceState(PounceState pState);
    public void ToggleScoop(bool toggleOn); // set whether to show the blue barrier when picking something up

    // Claw becomes active when jumping the wall
    // Before then it just sits at the home position
    // I assume this is for performance since the player won't see the claw when they're somewhere else
    public void SetActive(); // starts calling AiTick() on each frame
    public void SetDeactive(); // stop calling it

    // State (including config for default)
    public MentalState _defaultState { get; set; }
    public PounceState _pounceState { get; set; }
    public MentalState _mentalState { get; set; }

    // Config for movement
    public float _baseAcceleration { get; set; }
    public float acceleration { get; set; }
    public float speed { get; set; }

    // Config for when it is attempting to grab something
    public float pounceSpeedMult { get; set; }
    public float pounceAccelMult { get; set; }
    public float pounceSpringMult { get; set; }
    public float pounceDamperMult { get; set; }
    public float maxExtension { get; set; }
    public float extensionTime { get; set; }

    // Config for springiness when it's picking something up
    public float scoopSpringXz { get; set; }
    public float scoopDamperXz { get; set; }
    public float retractionTime { get; set; }

    // Physics (for moving the segments between the top and bottom of the claw)
    public Rigidbody _baseRb { get; set; }
    public ConfigurableJoint _jointBase { get; set; }
    public ConfigurableJoint _jointSpineA { get; set; }
    public ConfigurableJoint _jointSpineB { get; set; }
    public ConfigurableJoint _jointSpineC { get; set; }
    public ConfigurableJoint _jointCabin { get; set; }

    // Audio
    public UnityEngine.Audio.AudioMixerGroup _mixerGroup { get; set; }
    public AudioPlayer _articulationPlayer { get; set; }
    public AudioPlayer _movementPlayer { get; set; }
    public UnityEngine.AudioClip movementLoop { get; set; }
    public UnityEngine.AudioClip articulationLoop { get; set; }
    public UnityEngine.AudioClip charge { get; set; }
    public UnityEngine.AudioClip _scoopOn { get; set; }
    public UnityEngine.AudioClip _scoopOff { get; set; }
    public void AttenuateMovementLoop(ref AudioPlayer player, Transform parentTransform, float volume, float pitch);

    // Not totally sure about the rest of these
    public bool _forceAiTick { get; set; } // maybe AiTick() debounces and this bypasses that on next call?
    public LayerMask preyLayers { get; set; } // decides which objects it should try to pick up?
    public Dictionary<TriggerRefProxy, int> _targetRefCount { get; set; } // map of targets to something?
    public ulong _activeTargetId { get; set; } // some kind of ID for the current target?
    public float _targetExtension { get; set; } // how far it should extend downwards to pick up its target?
    public float _pounceTimer { get; set; } // for timing when to pounce again after missing a pounce?
    public Il2CppReferenceArray<Collider> _boxCheckResults { get; set; } // what objects are inside the claw?
    public Transform _boxCheck { get; set; } // the area inside the claw which counts as grabbed?
    public Transform unloadPoint { get; set; } // where to drop off grabbed objects?
    public Vector3 _scoopDisplace { get; set; }
    public Il2CppReferenceArray<GameObject> _scoopObjects { get; set; }
    public void Awake();
    public void CheckTarget();
    public void ClearPath(); // navigation?
    public void SetPath(Vector3 target); // navigation?
    public void SetScoopXzDrives(float spring, float damper); // how "diagonal" the claw is as it extends?
    public void TriggerStateChange(TriggerRefProxy trp, bool enter = true);
    public void Update();

    // The different possible states
    public enum MentalState
    {
        Rest = 0,
        Patrol = 1,
        Pounce = 2,
        Unload = 3
    }
    public enum PounceState
    {
        Charge = 0,
        Drop = 1,
        Scoop = 2,
        Check = 3
    }
}
```

Note that many of these types (eg. [`UnityEngine.AI.NavMeshAgent`](https://docs.unity3d.com/ScriptReference/AI.NavMeshAgent.html) or [`Vector3`](https://docs.unity3d.com/ScriptReference/Vector3.html)) can be looked up in the Unity documentation to understand how they work.

### 🧑‍💻 Building the mod

From this I had all the information I needed to build a mod which makes the boss claw patrol to the finish of the level 100% of the time. Using the `OnSceneWasInitialized` hook in the [MelonLoader mod framework](https://melonwiki.xyz/) I found the `BossClawAi` by using the standard Unity APIs (`FindObjectOfType()`) and simply changed the `_homePosition` to the point where I wanted the claw to go and set `patrolXz` to `(0, 0)` so it would always go to exactly that point.

Since there was the possibility of cheating runs with this mod (consciously or accidentally) I also had it paint the boss claw green so it's obvious the mod is active.

Also interesting to note here is that MelonLoader comes with a library called [Harmony](https://harmony.pardeike.net/) which allows "patching" methods. This replaces the code at the memory address of the method with a stub that calls your `Prefix` handler, the original method, then your `Postfix` handler. I didn't need to use it for my mod though, since I could do everything with standard Unity APIs and the built-in MelonLoader hooks.

### 🔍 Decompiling the code

I still wanted to know if the point it goes to was truly random though. To do this I used Cpp2IL to decompile the IL2CPP assembly back into IL using this command:

```
.\Cpp2IL-2022.0.5-Windows.exe --game-path "C:\Program Files (x86)\Steam\steamapps\common\BONEWORKS\BONEWORKS" --experimental-enable-il-to-assembly-please
```

The success rate of decompilation was 47% and unfortunately when I opened the output in dnSpy I found that `AiTick()` failed to decompile. ☹️

Output:

```
[Info] [Program] Overall analysis success rate: 47% (8617) of 18267 methods.
```

What dnSpy shows:

```c#
// Token: 0x0600074B RID: 1867 RVA: 0x00002050 File Offset: 0x00000250
[Token(Token = "0x600055C")]
[Address(RVA = "0x4EB1A0", Offset = "0x4E99A0", VA = "0x1804EB1A0")]
private void AiTick()
{
    throw new AnalysisFailedException("CPP2IL failed to recover any usable IL for this method.");
}
```

### 🔬 Disassembling the code

My backup plan was to read the assembly directly, so I loaded the game's assembly (`GameAssembly.dll`) into IDA. In addition to just converting the machine code, IDA analyses and works out the boundaries of all the subroutines and displays the disassembled code in a nice graph format. I ran Il2CppInspector on the game to find the address of `BossClawAi.AiTick()` and opened the subroutine graph at that address, since the logic for deciding where to go is probably in here. I knew that there should be a call to `NavMeshAgent.SetDestination()` just after it figures out the point it wants to patrol to so I searched for calls to its address in this subroutine and found a few of them. For each of these calls I worked backwards through the code, renaming any `call` instructions as I came across them to change the opaque memory address into a function name by cross-referencing the address with Il2CppInspector's output. Eventually I found what I think is the call that sends the boss claw to patrol.

The segment of assembly from IDA that sends the boss claw to a random point:

```asm
loc_1804EC38A:                          ; CODE XREF: sub_1804EB1A0+118C↑j
                                        ; sub_1804EB1A0+11D4↑j
                movss   xmm6, dword ptr [rdi+108h]
                xor     ecx, ecx
                call    Time_get_time
                comiss  xmm6, xmm0
                ja      loc_1804EBB03
                movss   xmm6, dword ptr [rdi+70h]
                xor     ecx, ecx
                call    Time_get_time
                movss   xmm1, cs:dword_1817BB6EC
                xor     ecx, ecx
                movsd   xmm8, qword ptr [rdi+118h]
                mov     ebx, [rdi+120h]
                divss   xmm1, xmm6
                addss   xmm0, xmm1
                movss   dword ptr [rdi+108h], xmm0
                call    Random_1_get_value        ; ========= Random patrol area X?
                movss   xmm6, dword ptr [rdi+68h]
                xor     ecx, ecx
                movaps  xmm7, xmm0
                call    Random_1_get_value        ; ========= Random patrol area Z?
                movss   xmm1, dword ptr [rdi+6Ch]
                lea     rcx, [rbp+57h+var_D0]
                movaps  xmm3, xmm1
                mulss   xmm7, xmm6
                xor     eax, eax
                mov     [rsp+110h+var_F0], r14
                mulss   xmm3, xmm0
                xorps   xmm2, xmm2
                mov     [rbp+57h+var_D0], rax
                addss   xmm7, xmm7
                mov     [rbp+57h+var_C8], eax
                addss   xmm3, xmm3
                subss   xmm7, xmm6
                subss   xmm3, xmm1
                movaps  xmm1, xmm7
                call    Coord_1_ToVector3
                mov     rcx, cs:qword_181EA6150
                test    byte ptr [rcx+127h], 2
                jz      short loc_1804EC446
                cmp     [rcx+0D8h], r14d
                jnz     short loc_1804EC446
                call    il2cpp_runtime_class_init_0
loc_1804EC446:                          ; CODE XREF: sub_1804EB1A0+1296↑j
                                        ; sub_1804EB1A0+129F↑j
                movsd   xmm0, [rbp+57h+var_D0]
                lea     r8, [rsp+110h+var_E0]
                mov     eax, [rbp+57h+var_C8]
                lea     rdx, [rbp+57h+var_C0]
                xor     r9d, r9d
                movsd   [rsp+110h+var_E0], xmm0
                lea     rcx, [rbp+57h+var_90]
                mov     [rsp+110h+var_D8], eax
                movsd   [rbp+57h+var_C0], xmm8
                mov     [rbp+57h+var_B8], ebx
                call    Vector3_op_Addition
                mov     rcx, [rdi+18h]
                test    rcx, rcx
                jz      short loc_1804EC4B2
                movsd   xmm0, qword ptr [rax]
                lea     rdx, [rsp+110h+var_E0]
                mov     eax, [rax+8]
                xor     r8d, r8d
                movsd   [rsp+110h+var_E0], xmm0
                mov     [rsp+110h+var_D8], eax
                call    BehaviourBaseNav_SetPath ; ========= Send boss claw to randomly chosen patrol point
                jmp     loc_1804EBB03
```

What I found was what I was expecting (a call to get a random value followed by `NavMeshAgent.SetDestination()`) but not what I was hoping for 😞 (some logic which could be manipulated to force it to go in a certain direction). Although it's important to note that I'm not 100% certain on this. Assembly is complicated. I haven't worked out the purpose of every single instruction and there are so many other code paths in the function. There may be some trick to trigger one of the other `NavMeshAgent.SetDestination()` calls to send it in the desired direction. 🤷

## 📃 Conclusion

Through this I've learnt a lot about reverse engineering. Sometimes you can get lucky and get C# code to read, other times it's a painful and manual process of reading assembly and cross-referencing against metadata memory addresses. But at this point I feel pretty confident to reverse engineer some aspect of a Unity game and what tools are available. Hope you've learnt a bit about this too from this post! 👍

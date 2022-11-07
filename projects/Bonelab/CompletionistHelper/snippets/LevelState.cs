var result = "";
foreach (var entry in SLZ.Data.DataManager.ActiveSave.Progression.LevelState) {
  result += "\n=== " + entry.key + "\n";
  foreach (var x in entry.value) {
    result += x.Key + ": " + x.value.ToString() + "\n";
  }
}
result;

// Invoked REPL, result:
// === Descent
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-f6f3-46e2-aa51-67214d656c65",
//   "BackRt": "c1534c5a-0c8a-4b82-9f8b-7a9543726f77",
//   "SideRt": "SLZ.BONELAB.Content.Spawnable.HandgunEder22training"
// }
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 416
// }
// SLZ.Bonelab.progress: 2350287559152
// SLZ.Bonelab.in_progress_inventory: {
//   "rightHand": "c1534c5a-8036-440a-8830-b99543686566"
// }
// SLZ.Bonelab.in_progress_ammo: {}

// === LongRun
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c",
//   "rightHand": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 176,
//   "medium": 218,
//   "heavy": 0
// }
// fa4645efba08b0a9b7bdff59f1a771559e8e69d7: yoinked
// fb1880e5d3622ffbb0adb69e29abc70af156e06e: yoinked
// 8a0109df6292e782097db55e2aeafe026a6f6024: yoinked
// SLZ.Bonelab.progress: 2350287559152
// 612e058010e8765d12ebbf673a90444d492e3dda: yoinked
// a4cfebd251b9b790ee0551232063ea593c7b9f23: yoinked
// a5fd6e18990fb0463db313a08a35c3891ad4e01e: yoinked
// ac8211b468ce5888c8dcf2c1158485774e9712c1: yoinked
// b4332d560b654a8da3729990ba13b57a69166fc1: yoinked
// 2adcf8042b702c9fe1797353ae28cf411f492d3d: yoinked
// d3fe56e7b13d12e93bc0545810c2bc688be0490a: yoinked
// 5347847ba63ef73e4d3070de5d449b4129c44eb7: yoinked
// 328b6f0a4fdc421e89dab86cbfb1da960b434bac: yoinked
// c7aab1598e84a9db1e88c24527162d03e4851b7f: yoinked

// === Ascent
// SLZ.Bonelab.final_inventory: {
//   "SideLf": "c1534c5a-8d03-42de-93c7-f595534d4755",
//   "BeltLf1": "c1534c5a-6e5b-4980-a3f2-95954d616761",
//   "BackLf": "c1534c5a-2774-48db-84fd-778447756e46",
//   "BackRt": "c1534c5a-4c47-428d-b5a5-b05747756e56",
//   "SideRt": "c1534c5a-8d03-42de-93c7-f595534d4755"
// }
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 466,
//   "medium": 640,
//   "heavy": 49
// }
// SLZ.Bonelab.progress: 2350287559152
// SLZ.Bonelab.in_progress_ammo: {
//   "light": 416,
//   "medium": 640,
//   "heavy": 76
// }
// SLZ.Bonelab.in_progress_inventory: {
//   "BeltLf1": "c1534c5a-8bb2-47cc-977a-46954d616761"
// }

// === Outro
// SLZ.Bonelab.final_inventory: {
//   "SideLf": "c1534c5a-03e2-409b-a089-127541756469",
//   "BeltLf1": "c1534c5a-6e5b-4980-a3f2-95954d616761",
//   "BackLf": "c1534c5a-2774-48db-84fd-778447756e46",
//   "BackRt": "c1534c5a-4c47-428d-b5a5-b05747756e56"
// }
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 241,
//   "medium": 86,
//   "heavy": 0
// }
// SLZ.Bonelab.completed: True
// bd9843031309886e6d705f57c9da7a96e84bb659: yoinked
// 9cd9894f2cedfe725299ce988de95c973fd93434: yoinked
// 5ce5ab14f5050ef25d8eddde608d64bed4ce6f3c: yoinked
// d12fb7e4105d3828cff5b7bf4536775386929c49: yoinked
// 2616c7f1f5d31121f709211b697838cac9480073: yoinked
// SLZ.Bonelab.progress: 2350287559152

// === Hub
// SLZ.Bonelab.ElevatorBroken: True
// SLZ.Bonelab.HubSpawnLocation: 2350287559152
// SLZ.Bonelab.ExperimentalKeyUnlocked: True
// SLZ.Bonelab.ModKeyUnlocked: True
// SLZ.Bonelab.TacTrialKeyUnlocked: True
// SLZ.Bonelab.ArenaKeyUnlocked: True
// SLZ.Bonelab.SandboxKeyUnlocked: True
// SLZ.Bonelab.ParkourKeyUnlocked: True
// SLZ.Bonelab.TacTrialKeyPlaced: True
// SLZ.Bonelab.ArenaKeyPlaced: True
// SLZ.Bonelab.SandboxKeyPlaced: True
// SLZ.Bonelab.ParkourKeyPlaced: True
// SLZ.Bonelab.ExperimentalKeyPlaced: True
// SLZ.Bonelab.ModKeyPlaced: True
// SLZ.Bonelab.JimmyKeyPlaced: True
// SLZ.Bonelab.BatteryAPlaced: True
// SLZ.Bonelab.BatteryBPlaced: True
// SLZ.Bonelab.JimmyKeyUnlocked: True

// === KartBowling
// SLZ.Bonelab.completed: True

// === Tuscany
// SLZ.Bonelab.completed: True

// === HalfwayPark
// SLZ.Bonelab.completed: True

// === Hub_A
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 50,
//   "medium": 640,
//   "heavy": 76
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-53c6-4aa3-8c88-93504d616761"
// }

// === MineDive
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.progress: 2350287559152
// SLZ.Bonelab.final_ammo_delta: {
//   "light": -11,
//   "medium": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "SideLf": "c1534c5a-2a4f-481f-8542-cc9545646572",
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-ccfa-4d99-af97-5e95534d474d",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c"
// }

// === BigAnomaly_A
// 5ed687007a08f0676682b31a95c3d9cb494909ad: yoinked
// 78fc6eeef27177aae73bf3c48852c51b4ce7fc63: yoinked
// 21b16f4249f9b99b883528aab546793577ceffbf: yoinked
// 9f02ac390b2ae2adebce991405e9bfed87172c21: yoinked
// 1b5e0d27072cfaa1625e648722171f00b8fdc4f9: yoinked
// cd1d76355cc50b98cde56f93dc1dfc8bc8dfa15a: yoinked
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.progress: 2350287559152
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 370,
//   "medium": -31,
//   "heavy": 76
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572",
//   "rightHand": "c1534c5a-03e2-409b-a089-127541756469"
// }

// === StreetPuncher
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": -38,
//   "medium": 0,
//   "heavy": -6
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === SprintBridge
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 0,
//   "medium": 0,
//   "heavy": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-8bb2-47cc-977a-46954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === MagmaGate
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 0,
//   "medium": 0,
//   "heavy": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-c061-4c5c-a5e2-3d955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === MoonBase
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_inventory: {
//   "SideLf": "c1534c5a-5747-42a2-bd08-ab3b47616467",
//   "BeltLf1": "c1534c5a-8bb2-47cc-977a-46954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-4b3e-4288-849c-ce955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === MonogonMotorway
// SLZ.Bonelab.sumOfLapsPB: 1.16119634264324E-311
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 0,
//   "medium": 0,
//   "heavy": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-4b3e-4288-849c-ce955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === Pillar
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.final_ammo_delta: {
//   "light": -2,
//   "medium": -160,
//   "heavy": -1
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-8bb2-47cc-977a-46954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-4b3e-4288-849c-ce955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === BigAnomaly_B
// SLZ.Bonelab.completed: True
// SLZ.Bonelab.progress: 2350287559152
// SLZ.Bonelab.final_ammo_delta: {
//   "light": 0,
//   "medium": -27,
//   "heavy": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-4b3e-4288-849c-ce955269666c",
//   "SideRt": "c1534c5a-2a4f-481f-8542-cc9545646572"
// }

// === Holodeck
// SLZ.Bonelab.completed: True

// === Hub_B
// SLZ.Bonelab.final_ammo_delta: {
//   "light": -3,
//   "medium": 0,
//   "heavy": 0
// }
// SLZ.Bonelab.final_inventory: {
//   "BeltLf1": "c1534c5a-e45e-4f53-a9ae-3c954d616761",
//   "BackLf": "c1534c5a-e0b5-4d4b-9df3-567147756e4d",
//   "BackRt": "c1534c5a-4b3e-4288-849c-ce955269666c",
//   "BackCt": "c1534c5a-03e2-409b-a089-127541756469",
//   "SideRt": "c1534c5a-03e2-409b-a089-127541756469"
// }

// === DungeonWarrior
// SLZ.Bonelab.completed: True

// === DistrictParkour
// SLZ.Bonelab.completed: True

// === FantasyArena
// SLZ.Bonelab.completed: True

// === Baseline
// SLZ.Bonelab.completed: True

// === GunRangeSandbox
// SLZ.Bonelab.completed: True

// === MuseumSandbox
// SLZ.Bonelab.completed: True

// === Mirror
// SLZ.Bonelab.completed: True

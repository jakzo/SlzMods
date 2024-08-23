using System;
using MelonLoader;

using Sst.HandTracking;

public class Preferences {
  public static MelonPreferences_Category Category;

  public static void Init() {
    Category = MelonPreferences.CreateCategory(Sst.HandTracking.BuildInfo.NAME);
  }

  public MelonPreferences_Entry<bool> HandLoco = Category.CreateEntry(
      "hand_locomotion", true, "Hand locomotion",
      "Allows locomotion by swinging hands in a running motion"
  );
  public MelonPreferences_Entry<bool> HeadLoco = Category.CreateEntry(
      "head_locomotion", true, "Head locomotion",
      "Allows locomotion by running on the spot in real life which the mod " +
          "tracks via head up/down bobbing"
  );
  public MelonPreferences_Entry<bool> ForwardsOnly = Category.CreateEntry(
      "forwards_only", true, "Locomotion forwards only",
      "Locks movement to forwards only (no strafing) to make controlling " +
          "direction easier"
  );
  public MelonPreferences_Entry<int> LockRunning = Category.CreateEntry(
      "run_lock_after_head_bobs", 9, "Run lock after number of head bobs",
      "Running will stay on after this number of head bobs (up + down is 2 " +
          "head bobs) or set to 0 to disable locking"
  );
  public MelonPreferences_Entry<int> WeaponRotationOffset =
      Category.CreateEntry(
          "weapon_rotation_offset", 20, "Weapon rotation offset",
          "Number of degrees hands rotate inwards when holding weapons " +
              "(improves trigger finger tracking accuracy)"
      );
  public MelonPreferences_Entry<bool> DebugShowHandTracking =
      Category.CreateEntry(
          "debug_show_hand_tracking", false,
          "[DEBUG] Show hand tracking visualization",
          "[DEBUG] Shows the joint rotations and positions returned by the " +
              "hand tracking API"
      );
  public MelonPreferences_Entry<bool> DebugShowJointRotations =
      Category.CreateEntry(
          "debug_show_joint_rotations", false, "[DEBUG] Show joint rotations",
          "[DEBUG] Shows the rotation of joints as text over the hand " +
              "tracking visualization"
      );
}

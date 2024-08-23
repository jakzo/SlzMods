using UnityEngine;

namespace Sst.HandTracking;

public class WeaponButtonDetector {
  private const float TRIGGER_TRAVEL_DIST = 0.03f;
  private const float TRIGGER_MIN_DIST_TO_WRIST = 0.03f;
  private const float NEAR_RADIUS = 0.03f;
  private const float FAR_RADIUS = 0.08f;
  private const float FAR_RADIUS_DIST = 0.08f;
  private const float MAX_DIST_UNDER_WRIST = -0.01f;

  private HandTracker _mainHand;
  private HandTracker _otherHand;
  private float _triggerStartDist;

  public WeaponButtonDetector(HandTracker mainHand, HandTracker otherHand) {
    _mainHand = mainHand;
    _otherHand = otherHand;
  }

  public bool IsTriggered() {
    var wrist =
        _mainHand.HandState.Joints[(int)OVRPlugin.BoneId.Hand_WristRoot];
    var index =
        _otherHand.HandState.Joints[(int)OVRPlugin.BoneId.Hand_IndexTip];
    var wristToIndex = index.TrackingPosition - wrist.TrackingPosition;
    var wristUpDir = wrist.TrackingRotation * Vector3.down;
    var indexToWristVerticalDist = Vector3.Dot(wristToIndex, wristUpDir);
    if (IsIndexInRadius(wrist, index, wristUpDir, indexToWristVerticalDist)) {
      if (indexToWristVerticalDist > _triggerStartDist) {
        _triggerStartDist = indexToWristVerticalDist;
      } else if (_triggerStartDist > 0f &&
                 indexToWristVerticalDist <= TRIGGER_MIN_DIST_TO_WRIST &&
                 _triggerStartDist - indexToWristVerticalDist >
                     TRIGGER_TRAVEL_DIST) {
        _triggerStartDist = 0f;
        return true;
      }
    } else {
      _triggerStartDist = 0f;
    }
    return false;
  }

  private bool IsIndexInRadius(
      JointTransform wrist, JointTransform index, Vector3 wristUpDir,
      float indexToWristVerticalDist
  ) {
    if (indexToWristVerticalDist < MAX_DIST_UNDER_WRIST)
      return false;
    var radius = indexToWristVerticalDist > 0f
        ? Mathf.LerpUnclamped(
              NEAR_RADIUS, FAR_RADIUS,
              indexToWristVerticalDist / FAR_RADIUS_DIST
          )
        : NEAR_RADIUS;
    var radiusSqr = radius * radius;
    var indexNearestWristUpPoint =
        wrist.TrackingPosition + indexToWristVerticalDist * wristUpDir;
    var indexToNearestWristUpDistSqr =
        (index.TrackingPosition - indexNearestWristUpPoint).sqrMagnitude;
    return indexToNearestWristUpDistSqr <= radiusSqr;
  }
}

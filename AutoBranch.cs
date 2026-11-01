/*
 * Standalone Auto Branch
 * Extracted from Poison Menu (GPL-3.0) - https://github.com/heycanihavethis/Poison
 * Copyright (C) 2026  Poison Software
 *
 * This program is free software: you can redistribute it and/or modify it under the
 * terms of the GNU General Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */

using GorillaLocomotion;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace AutoBranchMod
{
    public static class Movement
    {
        // ---- stand-ins for the bits of Poison that Auto Branch relied on ----

        // "Active" flag that Auto Branch reads. It is driven by the chosen input + mode below.
        public static bool rightGrab;

        // ---- activation input settings (editable from the GUI) ----
        public enum BranchInput { RightGrip, LeftGrip, A, B, X, Y, R3, L3, RightTrigger, LeftTrigger }

        public static readonly string[] BranchInputNames =
        {
            "Right Grip", "Left Grip", "A", "B", "X", "Y", "R3 (Right Stick)", "L3 (Left Stick)", "Right Trigger", "Left Trigger"
        };

        public static int branchInput = (int)BranchInput.RightGrip;
        public static bool branchToggleMode;   // false = hold to activate, true = press to toggle on/off
        public static bool branchToggled;
        private static bool lastInputDown;

        // Reads the chosen button: controller input, plus the same PC keys Poison uses
        public static bool IsBranchInputDown()
        {
            ControllerInputPoller c = ControllerInputPoller.instance;
            if (c == null) return false;

            Keyboard kb = Keyboard.current;
            bool key(Key k) => kb != null && kb[k].isPressed;

            bool stick(UnityEngine.XR.InputDevice d)
            {
                bool v = false;
                d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out v);
                return v;
            }

            switch ((BranchInput)branchInput)
            {
                case BranchInput.RightGrip: return c.rightGrab || key(Key.RightBracket);
                case BranchInput.LeftGrip: return c.leftGrab || key(Key.LeftBracket);
                case BranchInput.A: return c.rightControllerPrimaryButton || key(Key.E);
                case BranchInput.B: return c.rightControllerSecondaryButton || key(Key.R);
                case BranchInput.X: return c.leftControllerPrimaryButton || key(Key.F);
                case BranchInput.Y: return c.leftControllerSecondaryButton || key(Key.G);
                case BranchInput.R3: return stick(c.rightControllerDevice) || key(Key.Enter);
                case BranchInput.L3: return stick(c.leftControllerDevice) || key(Key.Enter);
                case BranchInput.RightTrigger: return ControllerInputPoller.TriggerFloat(XRNode.RightHand) >= 0.5f || key(Key.Equals);
                case BranchInput.LeftTrigger: return ControllerInputPoller.TriggerFloat(XRNode.LeftHand) >= 0.5f || key(Key.Minus);
            }
            return false;
        }

        // Call once per frame: turns the chosen input + hold/toggle mode into rightGrab
        public static void UpdateActivation()
        {
            bool down = IsBranchInputDown();

            // after hitting a tree (No Clip off) stay off until the input is let go / pressed again
            if (crashLock)
            {
                if (!down) crashLock = false;
                branchToggled = false;
                rightGrab = false;
                lastInputDown = down;
                return;
            }

            if (branchToggleMode)
            {
                if (down && !lastInputDown)
                    branchToggled = !branchToggled;
                rightGrab = branchToggled;
            }
            else
            {
                branchToggled = false;
                rightGrab = down;
            }

            lastInputDown = down;
        }

        public static void ResetActivation()
        {
            branchToggled = false;
            rightGrab = false;
        }

        private static bool crashLock;
        public static float crashStatusUntil;

        // Called when your body hits a tree with No Clip off: turns Auto Branch off until you activate it again
        public static void DisableFromCollision()
        {
            crashLock = true;
            crashStatusUntil = Time.time + 2f;
            ResetActivation();
        }

        private static readonly Dictionary<string, GameObject> objectPool = new Dictionary<string, GameObject>();

        public static GameObject GetObject(string find)
        {
            if (objectPool.TryGetValue(find, out GameObject go))
            {
                if (go != null)
                    return go;
                objectPool.Remove(find); // it was destroyed (scene change), look it up again
            }

            GameObject tgo = GameObject.Find(find);
            if (!tgo && find.Contains("/"))
            {
                var split = find.Split('/');
                var rootName = split[0];

                var root = GameObject.Find(rootName);

                if (root != null)
                {
                    var path = find[(rootName.Length + 1)..];
                    var tr = root.transform.Find(path);

                    if (tr != null)
                        tgo = tr.gameObject;
                }
            }
            if (tgo != null)
                objectPool.Add(find, tgo);

            return tgo;
        }

        private static int PlatformLayer => LayerMask.NameToLayer("BuilderProp");
        private static readonly HashSet<string> clipOwners = new HashSet<string>();
        private static GTPlayer clipPlayer;
        private static LayerMask clipLayers;
        private static Collider clipBodyCollider;
        private static Collider clipHeadCollider;
        private static bool clipBodyWasTrigger;
        private static bool clipHeadWasTrigger;
        public static void UpdateClipColliders(bool enabled, string owner = "Noclip")
        {
            if (enabled)
            {
                clipOwners.Remove(owner);
                if (clipOwners.Count > 0 || clipPlayer == null) return;
                clipPlayer.locomotionEnabledLayers = clipLayers;
                if (clipBodyCollider != null) clipBodyCollider.isTrigger = clipBodyWasTrigger;
                if (clipHeadCollider != null) clipHeadCollider.isTrigger = clipHeadWasTrigger;
                clipPlayer = null;
                clipBodyCollider = null;
                clipHeadCollider = null;
                return;
            }

            clipOwners.Add(owner);
            if (clipPlayer != null || GTPlayer.Instance == null) return;
            clipPlayer = GTPlayer.Instance;
            clipLayers = clipPlayer.locomotionEnabledLayers;
            clipBodyCollider = clipPlayer.bodyCollider;
            clipHeadCollider = clipPlayer.headCollider;
            if (clipBodyCollider != null)
            {
                clipBodyWasTrigger = clipBodyCollider.isTrigger;
                clipBodyCollider.isTrigger = true;
            }
            if (clipHeadCollider != null)
            {
                clipHeadWasTrigger = clipHeadCollider.isTrigger;
                clipHeadCollider.isTrigger = true;
            }
            clipPlayer.locomotionEnabledLayers = 1 << PlatformLayer;
        }

        // ---- Auto Branch ----
        //
        // Each hand is a small state machine instead of a per-frame lerp onto the branch:
        //   Free      - the hand follows your real tracked hand (plus a little idle arm drag)
        //   Reaching  - smooth reach (slow start, slow finish) toward the branch, never farther than arm length
        //   Gripping  - the hand holds the branch and acts like a rope, so you swing on it
        //   Releasing - the hand eases back to your tracked hand, or straight on to the next branch
        // Arm motion is measured from the shoulder, so the arm travels with your body while it reaches.
        // The body is accelerated toward the stick direction (never snapped), so your momentum carries through.

        private static readonly Vector3[] NoBranches = new Vector3[0];
        private static Vector3[] branchCache;
        private static GameObject branchProbe;
        private static float nextBranchBuild;

        // Every branch position in the forest. Built once and cached - the array is shared, don't modify it.
        public static Vector3[] GetAllTreeBranchPositions()
        {
            if (branchCache != null && branchProbe == null) // forest was unloaded, rebuild when it comes back
                branchCache = null;

            if (branchCache != null)
                return branchCache;

            if (Time.time < nextBranchBuild)
                return NoBranches;
            nextBranchBuild = Time.time + 1f;

            List<Vector3> list = new List<Vector3>(1024);

            Vector3[] TreeBranchOffsets = {
                new Vector3(-2.383f, 3.784f, 0.738f),
                new Vector3(1.55f, 5.559f, -1.56f),
                new Vector3(-2.225f, 7.214f, 0.063f),
                new Vector3(1.365f, 6.62f, 0.82f),
                new Vector3(0.405f, 8.865f, -2.759f),
                new Vector3(-2.227f, 9.763f, 2.071f),
                new Vector3(2.421f, 10.91f, 1.313f),
                new Vector3(1.618f, 13.169f, -1.216f),
                new Vector3(2.175f, 12.959f, -0.229f),
                new Vector3(1.855f, 13.837f, 1.215f),
                new Vector3(-0.265f, 14.953f, 2.935f),
                new Vector3(-2.049f, 14.962f, -1.708f),
                new Vector3(-1.249f, 18.93f, -1.62f),
            };

            string[] SmallTreeTargets = {
                "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/Group1",
                "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/Group2"
            };

            foreach (string SmallTreeTarget in SmallTreeTargets)
            {
                GameObject TreeGroupGO = GetObject(SmallTreeTarget);
                if (TreeGroupGO == null) continue;
                if (branchProbe == null) branchProbe = TreeGroupGO;

                for (int i = 0; i < TreeGroupGO.transform.childCount; i++)
                {
                    GameObject v = TreeGroupGO.transform.GetChild(i).gameObject;

                    Vector3 oldlocalscale = v.transform.localScale;
                    v.transform.localScale *= 5;

                    foreach (Vector3 TreeBranchOffset in TreeBranchOffsets)
                        list.Add(v.transform.TransformPoint(TreeBranchOffset));

                    v.transform.localScale = oldlocalscale;
                }
            }

            if (list.Count == 0)
                return NoBranches;

            branchCache = list.ToArray();
            return branchCache;
        }

        // Only the branches near you are scored each frame. This short list is refreshed a few times a second.
        private static readonly List<int> candidates = new List<int>(128);
        private static Vector3[] candidateSource;
        private static Vector3 candidateOrigin;
        private static float nextCandidateRefresh;

        private static void RefreshCandidates(Vector3[] branches, Vector3 bodyPos)
        {
            bool sameSource = object.ReferenceEquals(branches, candidateSource);
            if (sameSource && Time.time < nextCandidateRefresh && (bodyPos - candidateOrigin).sqrMagnitude < 1f)
                return;

            candidateSource = branches;
            candidateOrigin = bodyPos;
            nextCandidateRefresh = Time.time + 0.25f;
            candidates.Clear();

            float r = branchActivateDist + 3f;
            float rSqr = r * r;
            for (int i = 0; i < branches.Length; i++)
                if ((branches[i] - bodyPos).sqrMagnitude <= rSqr)
                    candidates.Add(i);
        }

        public static Vector3 leftPos = Vector3.zero;
        public static Vector3 rightPos = Vector3.zero;
        public static bool lastOnBranch;

        // Short text for the GUI status line ("Reaching", "Gripping", ...)
        public static string ArmStatus = "";

        // Auto Branch settings (editable from the Auto Branch GUI)
        public static float branchActivateDist = 5f;   // how close you must be to a branch for it to activate
        public static float branchSpeed = 10f;         // how hard you swing / push off

        public static float branchReach = 0.3f;        // reach speed: how quickly the hands travel to a branch
        public static float branchGrabDist = 1.1f;     // arm length: how far from your shoulder a branch can be grabbed
        public static float branchResponse = 9f;       // swing responsiveness: how fast your body follows the stick
        public static float branchGripHold = 0.6f;     // how firmly a gripped branch holds you (0 = free flying)
        public static float branchSmoothing = 0.6f;    // smooths steering and arm paths (0 = raw, 1 = very smooth)

        // How the hands travel to the branch
        public static readonly string[] BranchHandStyleNames = { "Natural", "Direct", "Sweep", "Off" };
        public static int branchHandStyle = 0;         // 0 Natural (gentle arc), 1 Direct (straight line), 2 Sweep (wide arc), 3 Off (hands stay put)

        // Which stick steers the fly direction (0 = left, 1 = right). PC: WASD
        public static int branchSteerStick = 0;

        public static Vector2 GetSteerStick()
        {
            ControllerInputPoller c = ControllerInputPoller.instance;
            Vector2 v = Vector2.zero;
            if (c != null)
                v = branchSteerStick == 0 ? c.leftControllerPrimary2DAxis : c.rightControllerPrimary2DAxis;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                float kx = (kb[Key.D].isPressed ? 1f : 0f) - (kb[Key.A].isPressed ? 1f : 0f);
                float ky = (kb[Key.W].isPressed ? 1f : 0f) - (kb[Key.S].isPressed ? 1f : 0f);
                if (kx != 0f || ky != 0f)
                    v = new Vector2(kx, ky).normalized;
            }
            return v;
        }

        // ---- arms ----

        public static float branchArmDrag = 0.5f;      // how much idle arms trail behind you (0 = off)

        private enum ArmPhase { Free, Reaching, Gripping, Releasing }

        private sealed class Arm
        {
            public readonly float side;                 // +1 right hand, -1 left hand
            public ArmPhase phase = ArmPhase.Free;
            public bool overriding;                     // true while we are writing this hand's transform
            public bool seeded;
            public Vector3 pos, prevPos, vel;           // where the hand is drawn and how fast it moves (world space)
            public Vector3 set, real;                   // last position we wrote / last real tracked position
            public int target = -1;                     // branch this hand has picked (-1 = none)
            public Vector3 targetPos;
            public int reachTarget = -1;                // branch the current reach / grip belongs to
            public float phaseStart, duration, nextPick, releasedUntil;
            public int lastReleased = -1;
            public Vector3 startRel, startVel;          // shoulder-relative start of the current reach / return

            public Arm(float side) { this.side = side; }
        }

        private struct FrameCtx
        {
            public float dt;
            public Vector3 headPos, bodyPos, bodyVel, bodyRight, bodyForward, moveDir;
            public bool stickActive;
            public Vector3[] branches;
        }

        private static readonly Arm rightArm = new Arm(1f);
        private static readonly Arm leftArm = new Arm(-1f);
        private static Vector3 dragSmoothed;

        private const float HandMaxStartSpeed = 6f;

        // Average hand speed of a reach, from the Reach Speed setting
        private static float ReachAvgSpeed => Mathf.Lerp(2.2f, 12f, Mathf.InverseLerp(0.05f, 1f, branchReach));

        // Sideways bulge of the reach path
        private static float ArcAmount => branchHandStyle == 1 ? 0f : (branchHandStyle == 2 ? 0.35f : 0.14f);

        private static Vector3 FlatForward() =>
            Vector3.ProjectOnPlane(GTPlayer.Instance.headCollider.transform.forward, Vector3.up).normalized;

        private static Vector3 ShoulderPos(Vector3 headPos, Vector3 bodyRight, float side) =>
            headPos + Vector3.down * 0.2f + bodyRight * (0.18f * side);

        // Idle arms trail slightly behind you: a little behind your body, and more the faster you move
        private static Vector3 ArmDrag(Vector3 vel, Vector3 flatForward)
        {
            if (branchArmDrag <= 0f) return Vector3.zero;

            Vector3 trail = Vector3.ClampMagnitude(-vel * 0.05f, 0.35f);
            Vector3 idle = -flatForward * 0.12f;
            return (trail + idle) * branchArmDrag;
        }

        // Smooth 0..1 curve with zero speed at both ends (minimum jerk): a natural slow start and slow finish
        private static float MinJerk(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        // Lets the hand's starting speed carry into the move, fading to nothing by the end (no velocity jump)
        private static float CarryShape(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return t * u * u;
        }

        public static float branchSwingRadius = 1.5f;  // how close to the branch the swing circles it
        public static float branchSwingCurve = 0.75f;  // how strongly you curve toward the branch
        public static bool branchStraight;             // true = no curve/swing, just straight along the stick
        public static bool branchPreferFront;          // true = hands favor branches in front of you over behind
        public static float branchReleaseBrake = 1f;   // 0 = keep every bit of momentum, 1 = firmly ease off excess speed after letting go

        // ---- more settings ----
        public static bool branchNoClip = true;        // true = pass through trees while on a branch, false = collide with them
        public static bool branchCrashDisable = true;  // No Clip off: hitting a tree with your body turns Auto Branch off
        public static float branchHitMargin = 0.05f;   // how close to a tree your body counts as "hit" (m)
        public static float branchHeadSnap = 0.7f;     // how aggressively your direction snaps to where your head points (0 = smooth, 1 = instant)
        public static float branchSpeedCap = 0f;       // hard speed limit while on a branch (0 = off)
        public static float branchVertical = 1f;       // strength of up / down movement
        public static float branchDeadzone = 0.15f;    // stick deadzone
        public static float branchStickCurve = 1f;     // stick response curve (<1 = more sensitive, >1 = finer control)
        public static float branchApproachAuth = 0.6f; // steering authority before you are holding a branch
        public static float branchGapGrace = 0.25f;    // how long a gap between branches may last before it counts as off a branch (s)
        public static float branchHandSpacing = 0.6f;  // how far apart the two hands' branches must be (m)
        public static float branchRegrab = 1f;         // how long before a released branch can be grabbed again (s)
        public static float branchReleaseStretch = 1.25f; // let go when the arm is stretched this many times its length
        public static bool branchHandOverHand = true;  // swap hands from one branch to the next
        public static float branchReleaseTime = 0.6f;  // how long the release easing lasts (s)
        public static bool branchAntiStick = true;     // swing mode lets go of branches you've steered past (off = original grip, can feel sticky)
        public static float branchStickiness = 0.33f;  // how long swing mode holds on before letting go (0 = lets go fast, 1 = very sticky)
        public static bool branchHeadAuto;             // automatically fly forward where your head points (replaces stick steering)
        public static bool guiSnow = true;             // snowfall in the GUI
        public static float guiOpacity = 0.92f;        // GUI panel opacity

        // ---- saved settings (auto saved / auto loaded) ----
        [Serializable]
        public class SavedSettings
        {
            public float activateDist, speed, reach, swingCurve, swingRadius, releaseBrake, armDrag;
            public float grabDist, response, gripHold, smoothing;
            public int handStyle, steerStick, input;
            public bool toggleMode, preferFront, straight;
            public float headSnap, speedCap, vertical, deadzone, stickCurve, approachAuth, gapGrace;
            public float handSpacing, regrab, releaseStretch, releaseTime, hitMargin, guiOpacity;
            public bool noClip, crashDisable, handOverHand, guiSnow;
            public float stickiness;
            public bool antiStick, headAuto;
        }

        private static string lastSavedJson;

        private static SavedSettings Capture() => new SavedSettings
        {
            activateDist = branchActivateDist, speed = branchSpeed, reach = branchReach,
            swingCurve = branchSwingCurve, swingRadius = branchSwingRadius, releaseBrake = branchReleaseBrake, armDrag = branchArmDrag,
            grabDist = branchGrabDist, response = branchResponse, gripHold = branchGripHold, smoothing = branchSmoothing,
            handStyle = branchHandStyle, steerStick = branchSteerStick, input = branchInput,
            toggleMode = branchToggleMode, preferFront = branchPreferFront, straight = branchStraight,
            headSnap = branchHeadSnap, speedCap = branchSpeedCap, vertical = branchVertical, deadzone = branchDeadzone,
            stickCurve = branchStickCurve, approachAuth = branchApproachAuth, gapGrace = branchGapGrace,
            handSpacing = branchHandSpacing, regrab = branchRegrab, releaseStretch = branchReleaseStretch,
            releaseTime = branchReleaseTime, hitMargin = branchHitMargin, guiOpacity = guiOpacity,
            noClip = branchNoClip, crashDisable = branchCrashDisable, handOverHand = branchHandOverHand, guiSnow = guiSnow,
            stickiness = branchStickiness, antiStick = branchAntiStick, headAuto = branchHeadAuto
        };

        private static string SettingsPath => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "AutoBranch.json");

        public static void LoadSettings()
        {
            try
            {
                if (System.IO.File.Exists(SettingsPath))
                {
                    SavedSettings saved = Capture(); // start from defaults, file overrides what it has
                    JsonUtility.FromJsonOverwrite(System.IO.File.ReadAllText(SettingsPath), saved);

                    branchActivateDist = Mathf.Clamp(saved.activateDist, 0.5f, 15f);
                    branchSpeed = Mathf.Clamp(saved.speed, 1f, 40f);
                    branchReach = Mathf.Clamp(saved.reach, 0.05f, 1f);
                    branchGrabDist = Mathf.Clamp(saved.grabDist, 0.4f, 2.5f);
                    branchResponse = Mathf.Clamp(saved.response, 1f, 30f);
                    branchGripHold = Mathf.Clamp01(saved.gripHold);
                    branchSmoothing = Mathf.Clamp01(saved.smoothing);
                    branchSwingCurve = Mathf.Clamp(saved.swingCurve, 0f, 2f);
                    branchSwingRadius = Mathf.Clamp(saved.swingRadius, 0.5f, 4f);
                    branchReleaseBrake = Mathf.Clamp01(saved.releaseBrake);
                    branchArmDrag = Mathf.Clamp01(saved.armDrag);
                    branchHandStyle = Mathf.Clamp(saved.handStyle, 0, BranchHandStyleNames.Length - 1);
                    branchSteerStick = Mathf.Clamp(saved.steerStick, 0, 1);
                    branchInput = Mathf.Clamp(saved.input, 0, BranchInputNames.Length - 1);
                    branchToggleMode = saved.toggleMode;
                    branchPreferFront = saved.preferFront;
                    branchStraight = saved.straight;
                    branchHeadSnap = Mathf.Clamp01(saved.headSnap);
                    branchSpeedCap = Mathf.Clamp(saved.speedCap, 0f, 60f);
                    branchVertical = Mathf.Clamp(saved.vertical, 0.25f, 2f);
                    branchDeadzone = Mathf.Clamp(saved.deadzone, 0.05f, 0.5f);
                    branchStickCurve = Mathf.Clamp(saved.stickCurve, 0.5f, 2.5f);
                    branchApproachAuth = Mathf.Clamp(saved.approachAuth, 0.2f, 1f);
                    branchGapGrace = Mathf.Clamp(saved.gapGrace, 0f, 1f);
                    branchHandSpacing = Mathf.Clamp(saved.handSpacing, 0.1f, 2f);
                    branchRegrab = Mathf.Clamp(saved.regrab, 0f, 3f);
                    branchReleaseStretch = Mathf.Clamp(saved.releaseStretch, 1.05f, 2f);
                    branchReleaseTime = Mathf.Clamp(saved.releaseTime, 0.1f, 2f);
                    branchHitMargin = Mathf.Clamp(saved.hitMargin, 0f, 0.3f);
                    guiOpacity = Mathf.Clamp(saved.guiOpacity, 0.4f, 1f);
                    branchNoClip = saved.noClip;
                    branchCrashDisable = saved.crashDisable;
                    branchHandOverHand = saved.handOverHand;
                    guiSnow = saved.guiSnow;
                    branchStickiness = Mathf.Clamp01(saved.stickiness);
                    branchAntiStick = saved.antiStick;
                    branchHeadAuto = saved.headAuto;
                }
            }
            catch (Exception) { }

            lastSavedJson = JsonUtility.ToJson(Capture(), true);
        }

        public static void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(Capture(), true);
                if (json == lastSavedJson) return;

                System.IO.File.WriteAllText(SettingsPath, json);
                lastSavedJson = json;
            }
            catch (Exception) { }
        }

        private static float FrontBias(Vector3 toBranch, Vector3 bodyForward) =>
            branchPreferFront && Vector3.Dot(toBranch, bodyForward) < 0f ? 3f : 1f;

        // ---- branch targeting ----

        // Lower is better. float.MaxValue = not usable by this hand right now.
        private static float ScoreBranch(int i, Arm arm, Arm other, in FrameCtx c)
        {
            if (i < 0 || i >= c.branches.Length) return float.MaxValue;
            if (i == other.target) return float.MaxValue;

            float now = Time.time;
            if (i == arm.lastReleased && now < arm.releasedUntil) return float.MaxValue;     // don't grab what you just let go of
            if (i == other.lastReleased && now < other.releasedUntil) return float.MaxValue;

            Vector3 p = c.branches[i];
            if (other.target >= 0 && (p - other.targetPos).sqrMagnitude < branchHandSpacing * branchHandSpacing) return float.MaxValue; // hands stay apart

            // distance is measured from this specific hand, and only branches inside the radius count
            Vector3 toB = p - arm.real;
            float dSqr = toB.sqrMagnitude;
            float limit = i == arm.target ? branchActivateDist + 0.75f : branchActivateDist;      // a held pick gets some slack
            if (dSqr > limit * limit) return float.MaxValue;

            // the closest branch to this hand wins
            float score = Mathf.Sqrt(dSqr);

            if (i == arm.target) score *= 0.7f; // stickiness, so the pick doesn't flicker between two similar branches
            return score;
        }

        private static int PickBranch(Arm arm, Arm other, in FrameCtx c)
        {
            int best = -1;
            float bestScore = float.MaxValue;

            if (arm.target >= 0)
            {
                float s = ScoreBranch(arm.target, arm, other, in c);
                if (s < bestScore) { bestScore = s; best = arm.target; }
            }

            for (int k = 0; k < candidates.Count; k++)
            {
                int i = candidates[k];
                if (i == arm.target) continue;

                float s = ScoreBranch(i, arm, other, in c);
                if (s < bestScore) { bestScore = s; best = i; }
            }

            return best;
        }

        // Should this hand start reaching for its branch yet?
        private static bool ShouldReach(Arm arm, Vector3 shoulder, in FrameCtx c)
        {
            // the hand's branch is inside the radius: always head for it
            if ((arm.targetPos - arm.real).sqrMagnitude <= (branchActivateDist + 0.75f) * (branchActivateDist + 0.75f)) return true;

            float reach = branchGrabDist + 0.1f;
            Vector3 toT = arm.targetPos - shoulder;
            if (toT.sqrMagnitude <= reach * reach) return true;

            // flying toward it: start early enough that the arm arrives as the branch comes into range
            Vector3 seg = c.bodyVel * 0.3f;
            float segSqr = seg.sqrMagnitude;
            if (segSqr < 0.01f) return false;

            float along = Mathf.Clamp01(Vector3.Dot(toT, seg) / segSqr);
            return (toT - seg * along).sqrMagnitude <= reach * reach;
        }

        // Should a gripping hand let go?
        private static bool ShouldLetGo(Arm arm, Arm other, Vector3 shoulder, in FrameCtx c)
        {
            float hold = Time.time - arm.phaseStart;
            if (hold < 0.2f) return false;

            Vector3 toG = arm.targetPos - shoulder;
            float L = toG.magnitude;
            if (L < 0.01f) return false;

            if (L > branchGrabDist * branchReleaseStretch) return true;   // stretched too far to keep holding on
            if (!c.stickActive) return false;               // no stick input: just hang on

            float closing = Vector3.Dot(c.bodyVel, toG / L); // + moving toward the branch, - moving away from it
            if (closing < -0.5f && L > branchGrabDist * 0.45f) return true;                         // swung past it: let go

            // Swing mode circles the branch sideways, so "moving away" never shows up and the hand stays stuck.
            // Let go once you aim past the branch, or once you've been circling it without getting anywhere.
            if (branchAntiStick && !branchStraight)
            {
                float intent = Vector3.Dot(c.moveDir, toG / L); // + steering toward the branch, - steering away from it
                float intentLimit = Mathf.Lerp(0.1f, -0.5f, branchStickiness); // sticky = you must aim well past the branch
                float holdLimit = Mathf.Lerp(0.4f, 3f, branchStickiness);      // sticky = circle for longer before letting go
                if (intent < intentLimit && L > branchGrabDist * 0.45f) return true;
                if (hold > holdLimit && closing < 0.5f) return true;
            }
            if (branchHandOverHand && other.phase == ArmPhase.Gripping && hold > 0.25f && closing < 1.5f) return true;   // hand over hand
            return false;
        }

        private static void BeginReach(Arm arm, Vector3 shoulder, in FrameCtx c)
        {
            arm.phase = ArmPhase.Reaching;
            arm.phaseStart = Time.time;
            arm.reachTarget = arm.target;

            // start from exactly where the hand is, with the speed it already has
            arm.startRel = arm.pos - shoulder;
            arm.startVel = Vector3.ClampMagnitude(arm.vel - c.bodyVel, HandMaxStartSpeed);

            Vector3 toT = arm.targetPos - shoulder;
            float len = Mathf.Min(toT.magnitude, branchGrabDist);
            Vector3 endRel = toT.sqrMagnitude > 0.0001f ? toT.normalized * len : Vector3.zero;
            float travel = (endRel - arm.startRel).magnitude;
            arm.duration = Mathf.Clamp(0.08f + travel / ReachAvgSpeed, 0.16f, 0.8f);
        }

        private static void BeginRelease(Arm arm, Vector3 shoulder, Vector3 real, in FrameCtx c)
        {
            arm.phase = ArmPhase.Releasing;
            arm.phaseStart = Time.time;

            arm.startRel = arm.pos - shoulder;
            arm.startVel = Vector3.ClampMagnitude(arm.vel - c.bodyVel, HandMaxStartSpeed);

            float travel = (real + dragSmoothed - shoulder - arm.startRel).magnitude;
            arm.duration = Mathf.Clamp(0.1f + travel / (ReachAvgSpeed * 0.8f), 0.18f, 0.7f);
        }

        private static void UpdateArm(Arm arm, Arm other, Transform hand, bool active, bool applyHands, in FrameCtx c)
        {
            float dt = c.dt;
            float now = Time.time;

            // The hand's real (tracked) position, even though we overwrite the transform every frame
            Vector3 cur = hand.position;
            if (!arm.overriding || (cur - arm.set).sqrMagnitude > 0.000001f)
                arm.real = cur;
            Vector3 real = arm.real;

            if (!arm.seeded)
            {
                arm.pos = arm.prevPos = real;
                arm.seeded = true;
            }

            Vector3 shoulder = ShoulderPos(c.headPos, c.bodyRight, arm.side);
            Vector3 aim = real + dragSmoothed; // where a free hand belongs

            // ---- which branch does this hand want? ----
            if (!active)
            {
                arm.target = -1;
            }
            else
            {
                if (arm.target >= 0)
                {
                    float lim = branchActivateDist + 0.75f;
                    if ((real - arm.targetPos).sqrMagnitude > lim * lim)
                        arm.target = -1; // out of range
                }

                if (now >= arm.nextPick && arm.phase != ArmPhase.Gripping)
                {
                    arm.nextPick = now + 0.12f;

                    int pick = PickBranch(arm, other, in c);
                    if (pick != arm.target)
                    {
                        arm.target = pick;
                        if (pick >= 0) arm.targetPos = c.branches[pick];
                    }
                }
            }

            // ---- phase changes ----
            bool hasTarget = arm.target >= 0;
            switch (arm.phase)
            {
                case ArmPhase.Free:
                case ArmPhase.Releasing:
                    // a releasing hand can go straight on to the next branch instead of returning to the body first
                    if (hasTarget && (arm.phase == ArmPhase.Free || now - arm.phaseStart > arm.duration * 0.3f)
                        && ShouldReach(arm, shoulder, in c))
                        BeginReach(arm, shoulder, in c);
                    break;

                case ArmPhase.Reaching:
                    if (!hasTarget)
                        BeginRelease(arm, shoulder, real, in c);
                    else if (arm.target != arm.reachTarget)
                        BeginReach(arm, shoulder, in c); // changed its mind: restart smoothly from where the hand is now
                    break;

                case ArmPhase.Gripping:
                    if (!hasTarget || ShouldLetGo(arm, other, shoulder, in c))
                    {
                        arm.lastReleased = arm.reachTarget;
                        arm.releasedUntil = now + branchRegrab;
                        arm.target = -1;
                        arm.nextPick = now + 0.1f;
                        BeginRelease(arm, shoulder, real, in c);
                    }
                    break;
            }

            // ---- hand motion ----
            switch (arm.phase)
            {
                case ArmPhase.Free:
                    arm.pos = aim;
                    break;

                case ArmPhase.Reaching:
                {
                    float t = (now - arm.phaseStart) / arm.duration;
                    Vector3 toT = arm.targetPos - shoulder;
                    float dT = toT.magnitude;
                    float reach = branchGrabDist;

                    // aim for the branch, but the arm can only stretch so far from the shoulder
                    Vector3 endRel = dT > reach ? toT * (reach / dT) : toT;

                    Vector3 rel = Vector3.Lerp(arm.startRel, endRel, MinJerk(t));
                    rel += arm.startVel * (arm.duration * CarryShape(t));

                    float arc = ArcAmount;
                    if (dT > 0.05f && t < 1f && (arc > 0f || branchPreferFront))
                    {
                        Vector3 outward = Vector3.Cross(Vector3.up, toT / dT) * arm.side;
                        float bulge = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * Mathf.Min(dT, reach);
                        rel += (outward * arc + Vector3.up * (arc * 0.4f)) * bulge;
                        if (branchPreferFront)
                            rel += c.bodyForward * (bulge * 0.3f);
                    }

                    arm.pos = shoulder + rel;

                    if (t >= 1f)
                    {
                        if (dT <= reach + 0.005f)
                        {
                            // the hand has arrived exactly on the branch: grab it
                            arm.phase = ArmPhase.Gripping;
                            arm.phaseStart = now;
                            arm.pos = arm.targetPos;
                        }
                        else if (now - arm.phaseStart > arm.duration + 1.2f)
                        {
                            // never got close enough: give up on this one for a moment
                            arm.lastReleased = arm.reachTarget;
                            arm.releasedUntil = now + branchRegrab;
                            arm.target = -1;
                            BeginRelease(arm, shoulder, real, in c);
                        }
                    }
                    break;
                }

                case ArmPhase.Gripping:
                    arm.pos = arm.targetPos; // the hand stays put on the branch while your body swings
                    break;

                case ArmPhase.Releasing:
                {
                    float t = (now - arm.phaseStart) / arm.duration;
                    Vector3 rel = Vector3.Lerp(arm.startRel, aim - shoulder, MinJerk(t));
                    rel += arm.startVel * (arm.duration * CarryShape(t));
                    arm.pos = shoulder + rel;

                    if (t >= 1f)
                    {
                        arm.phase = ArmPhase.Free;
                        arm.pos = aim;
                    }
                    break;
                }
            }

            // ---- write the hand (only while we need to, so tracking is untouched the rest of the time) ----
            bool settled = !active && arm.phase == ArmPhase.Free && dragSmoothed.sqrMagnitude < 0.000001f;

            arm.overriding = applyHands && !settled;
            if (arm.overriding)
            {
                hand.position = arm.pos;
                arm.set = hand.position;
            }
            else
            {
                arm.set = hand.position;
            }

            // hand speed, so the next reach / return can start from it
            Vector3 v = (arm.pos - arm.prevPos) / dt;
            arm.vel = Vector3.Lerp(arm.vel, v, 0.6f);
            arm.prevPos = arm.pos;
        }

        private static void ConsiderAnchor(Arm arm, Vector3 bodyPos, ref bool found, ref float bestRank, ref Vector3 anchor)
        {
            if (arm.target < 0) return;

            float d = Vector3.Distance(bodyPos, arm.targetPos);
            float rank = arm.phase == ArmPhase.Gripping ? d - 100f : d; // a held branch always wins
            if (rank < bestRank)
            {
                bestRank = rank;
                anchor = arm.targetPos;
                found = true;
            }
        }

        // The stick sets the direction; near the branch you curve toward it and then circle around it.
        private static Vector3 SwingDirection(Vector3 dir, Vector3 anchor, Vector3 bodyPos)
        {
            if (branchStraight) return dir;

            Vector3 toBranch = anchor - bodyPos;
            float r = toBranch.magnitude;
            if (r <= 0.001f) return dir;

            float radius = branchSwingRadius;
            Vector3 toward = toBranch / r;

            // far from the branch: curve toward it (fades out as you reach the radius)
            float far = Mathf.Clamp01((r - radius) / radius);
            Vector3 pulled = (dir + toward * branchSwingCurve * far).normalized;

            // inside the radius: circle around it, nudged outward so you never run into it
            Vector3 around = Vector3.ProjectOnPlane(dir, toward);
            around = around.sqrMagnitude > 0.001f ? around.normalized : dir;
            Vector3 swung = (around - toward * Mathf.Clamp01((radius - r) / radius)).normalized;

            // one smooth blend between the two - no sudden switch at the radius
            Vector3 blended = Vector3.Lerp(pulled, swung, 1f - far);
            return blended.sqrMagnitude > 0.001f ? blended.normalized : dir;
        }

        // A gripped branch works like a rope: it can't stretch, so you swing on it instead of flying through it
        private static void ApplyRope(Arm arm, Vector3 headPos, Vector3 bodyRight, float dt, ref Vector3 vel, ref bool changed)
        {
            if (arm.phase != ArmPhase.Gripping || branchGripHold <= 0f) return;

            Vector3 toG = arm.targetPos - ShoulderPos(headPos, bodyRight, arm.side);
            float L = toG.magnitude;
            float rope = branchGrabDist * 0.85f;
            if (L <= rope || L < 0.001f) return;

            Vector3 n = toG / L;
            float hold = branchGripHold;

            // reel the body in gently the further it is past the rope length
            float pull = Mathf.Min((L - rope) * (80f * hold), 40f);
            vel += n * (pull * dt);

            // and remove the speed that would stretch the rope (the rest of your speed becomes the swing)
            float outward = -Vector3.Dot(vel, n);
            if (outward > 0f)
                vel += n * (outward * (1f - Mathf.Exp(-18f * hold * dt)));

            changed = true;
        }

        // ---- tree collision (used when No Clip is off) ----
        private static readonly Collider[] hitBuffer = new Collider[48];
        private static readonly string[] treeGroupPaths =
        {
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/Group1",
            "Environment Objects/LocalObjects_Prefab/Forest/Terrain/SmallTrees/Group2"
        };

        private static bool IsTreeCollider(Collider col)
        {
            for (int i = 0; i < treeGroupPaths.Length; i++)
            {
                GameObject g = GetObject(treeGroupPaths[i]);
                if (g != null && col.transform.IsChildOf(g.transform)) return true;
            }
            return false;
        }

        // Is your body touching (or within the hit margin of) a tree?
        private static bool BodyHitsTree(Collider body)
        {
            if (body == null) return false;

            int n;
            CapsuleCollider cap = body as CapsuleCollider;
            if (cap != null)
            {
                Transform t = body.transform;
                Vector3 s = t.lossyScale;
                Vector3 axis;
                float axisScale, radScale;
                if (cap.direction == 0) { axis = t.right; axisScale = Mathf.Abs(s.x); radScale = Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)); }
                else if (cap.direction == 1) { axis = t.up; axisScale = Mathf.Abs(s.y); radScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z)); }
                else { axis = t.forward; axisScale = Mathf.Abs(s.z); radScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y)); }

                float r = cap.radius * radScale;
                float half = Mathf.Max(0f, cap.height * 0.5f * axisScale - r);
                Vector3 center = t.TransformPoint(cap.center);
                n = Physics.OverlapCapsuleNonAlloc(center + axis * half, center - axis * half, r + branchHitMargin, hitBuffer, ~0, QueryTriggerInteraction.Ignore);
            }
            else
            {
                Bounds b = body.bounds;
                n = Physics.OverlapBoxNonAlloc(b.center, b.extents + Vector3.one * branchHitMargin, hitBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            }

            for (int i = 0; i < n; i++)
            {
                Collider col = hitBuffer[i];
                if (col == null || col == body) continue;
                if (IsTreeCollider(col)) return true;
            }
            return false;
        }

        private static bool clipActive;
        private static Vector3 lastAnchor;
        private static float graceUntil;
        private static float easeEnd;
        private static float engage = 0.6f;
        private static Vector3 steerDir;
        private static bool steerActive;

        public static void AutoBranch()
        {
            AutoBranchGUI.Ping();

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            GorillaTagger tagger = GorillaTagger.Instance;
            Rigidbody body = tagger.rigidbody;
            Transform bodyT = tagger.bodyCollider.transform;
            Transform head = GTPlayer.Instance.headCollider.transform;

            bool active = rightGrab;
            float now = Time.time;

            Vector3 bodyPos = bodyT.position;
            Vector3 vel = body.linearVelocity;
            Vector3 bodyForward = FlatForward();
            Vector3 bodyRight = Vector3.ProjectOnPlane(bodyT.right, Vector3.up);
            bodyRight = bodyRight.sqrMagnitude > 0.0001f ? bodyRight.normalized : Vector3.Cross(Vector3.up, bodyForward);

            // stick input: sets the direction relative to where you face (looking up or down steers you up or down)
            Vector2 stick = active ? (branchHeadAuto ? Vector2.up : GetSteerStick()) : Vector2.zero; // Head Auto: always full forward, stick ignored
            bool stickActive = stick.magnitude > branchDeadzone;
            Vector3 headRight = Vector3.ProjectOnPlane(head.right, Vector3.up).normalized;
            Vector3 stickDir = stickActive ? (head.forward * stick.y + headRight * stick.x).normalized : Vector3.zero;

            // where you're heading: used to rank branches
            Vector3 moveDir = stickActive ? stickDir : (vel.sqrMagnitude > 2f ? vel.normalized : bodyForward);

            Vector3[] branches = NoBranches;
            if (active)
            {
                branches = GetAllTreeBranchPositions();
                RefreshCandidates(branches, bodyPos);
            }

            FrameCtx c = new FrameCtx
            {
                dt = dt,
                headPos = head.position,
                bodyPos = bodyPos,
                bodyVel = vel,
                bodyRight = bodyRight,
                bodyForward = bodyForward,
                moveDir = moveDir,
                stickActive = stickActive,
                branches = branches
            };

            // idle arm drag eases in and out instead of popping
            Vector3 dragTarget = active ? ArmDrag(vel, bodyForward) : Vector3.zero;
            dragSmoothed = Vector3.Lerp(dragSmoothed, dragTarget, 1f - Mathf.Exp(-8f * dt));
            if (dragTarget == Vector3.zero && dragSmoothed.sqrMagnitude < 0.0000001f)
                dragSmoothed = Vector3.zero;

            // ---- arms ----
            bool applyHands = branchHandStyle != 3; // Off: hands stay on your real controllers
            UpdateArm(rightArm, leftArm, tagger.rightHandTransform, active, applyHands, in c);
            UpdateArm(leftArm, rightArm, tagger.leftHandTransform, active, applyHands, in c);
            rightPos = rightArm.pos;
            leftPos = leftArm.pos;

            // ---- which branch steers the body ----
            bool hasTarget = false;
            float bestRank = float.MaxValue;
            Vector3 anchor = lastAnchor;
            ConsiderAnchor(rightArm, bodyPos, ref hasTarget, ref bestRank, ref anchor);
            ConsiderAnchor(leftArm, bodyPos, ref hasTarget, ref bestRank, ref anchor);

            bool anyGrip = rightArm.phase == ArmPhase.Gripping || leftArm.phase == ArmPhase.Gripping;
            bool anyReach = rightArm.phase == ArmPhase.Reaching || leftArm.phase == ArmPhase.Reaching;

            if (active && hasTarget)
            {
                lastAnchor = anchor;
                graceUntil = now + branchGapGrace; // brief gap between branches doesn't toggle noclip on and off
            }
            if (!active) graceUntil = 0f;

            bool isOnBranch = active && (hasTarget || now < graceUntil);
            ArmStatus = !active ? "" : anyGrip ? "Gripping" : anyReach ? "Reaching" : isOnBranch ? "Approaching" : "";

            // With No Clip off you collide with trees: hitting one with your body turns Auto Branch off
            if (isOnBranch && !branchNoClip && branchCrashDisable && BodyHitsTree(tagger.bodyCollider))
            {
                DisableFromCollision();
                isOnBranch = false;
                graceUntil = 0f;
                ArmStatus = "";
            }

            // ---- body ----
            bool velChanged = false;

            if (isOnBranch)
            {
                // steering has full authority once you're holding on, a bit less while still on approach
                float engageTarget = anyGrip ? 1f : (anyReach ? Mathf.Lerp(branchApproachAuth, 1f, 0.5f) : branchApproachAuth);
                engage = Mathf.Lerp(engage, engageTarget, 1f - Mathf.Exp(-6f * dt));

                if (stickActive)
                {
                    Vector3 dir = SwingDirection(stickDir, anchor, bodyPos);

                    // the steering direction turns smoothly, starting from the way you're already moving
                    if (!steerActive)
                    {
                        // head follow: the more aggressive it is, the more it starts straight from where you look instead of your old heading
                        steerDir = vel.sqrMagnitude > 1f ? Vector3.Slerp(vel.normalized, dir, branchHeadSnap) : dir;
                        steerActive = true;
                    }
                    float turn = Mathf.Lerp(45f, 5f, branchSmoothing) * Mathf.Lerp(1f, 3f, branchHeadSnap); // head follow turns the heading faster
                    steerDir = Vector3.Slerp(steerDir, dir, 1f - Mathf.Exp(-turn * dt));
                    steerDir = steerDir.sqrMagnitude > 0.0001f ? steerDir.normalized : dir;

                    // accelerate toward the target velocity: it keeps your current velocity and changes it gradually
                    float mag = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(branchDeadzone, 0.9f, stick.magnitude));
                    mag = Mathf.Pow(mag, branchStickCurve);
                    Vector3 desired = steerDir * (branchSpeed * mag * engage);
                    desired.y *= branchVertical;
                    float resp = branchResponse * Mathf.Lerp(1f, 2f, branchHeadSnap); // head follow also makes the body catch up faster
                    Vector3 dv = (desired - vel) * (1f - Mathf.Exp(-resp * dt));
                    float maxDv = (8f + resp * 4f) * dt;
                    if (dv.sqrMagnitude > maxDv * maxDv)
                        dv = dv.normalized * maxDv;

                    vel += dv;
                    velChanged = true;
                }
                else
                {
                    steerActive = false;
                }

                ApplyRope(rightArm, c.headPos, bodyRight, dt, ref vel, ref velChanged);
                ApplyRope(leftArm, c.headPos, bodyRight, dt, ref vel, ref velChanged);

                if (branchSpeedCap > 0f && vel.sqrMagnitude > branchSpeedCap * branchSpeedCap)
                {
                    vel = vel.normalized * branchSpeedCap;
                    velChanged = true;
                }
            }
            else
            {
                steerActive = false;
                engage = branchApproachAuth;
            }

            // Activation just ended: ease off any excess speed so you aren't flung, but keep your normal momentum
            if (!isOnBranch && lastOnBranch)
                easeEnd = now + Mathf.Max(0.05f, branchReleaseTime);

            if (!isOnBranch && now < easeEnd && branchReleaseBrake > 0f)
            {
                float fade = Mathf.Clamp01((easeEnd - now) / Mathf.Max(0.05f, branchReleaseTime));
                float cap = Mathf.Max(branchSpeed * 1.25f, 8f);
                float rate = 1f - Mathf.Exp(-8f * branchReleaseBrake * fade * dt);

                float flatSpeed = new Vector2(vel.x, vel.z).magnitude;
                if (flatSpeed > cap)
                {
                    float scale = (flatSpeed - (flatSpeed - cap) * rate) / flatSpeed;
                    vel.x *= scale;
                    vel.z *= scale;
                    velChanged = true;
                }
                if (vel.y > cap)
                {
                    vel.y -= (vel.y - cap) * rate;
                    velChanged = true;
                }
            }

            if (velChanged)
                body.linearVelocity = vel;

            // No Clip: only clip through trees while on a branch, and only if the toggle is on
            bool wantClip = isOnBranch && branchNoClip;
            if (wantClip && !clipActive)
            {
                UpdateClipColliders(false);
                clipActive = true;
            }
            else if (!wantClip && clipActive)
            {
                UpdateClipColliders(true);
                clipActive = false;
            }

            lastOnBranch = isOnBranch;
        }

        // Clean custom GUI for Auto Branch: dark glass panel, faint animated snowfall, two sliders.
        // Toggle with Insert. Only shows while Auto Branch is enabled.
        public class AutoBranchGUI : MonoBehaviour
        {
            private static AutoBranchGUI instance;
            private static float lastPing = -10f;
            public static Key ToggleKey = Key.B;

            public static void Ping()
            {
                lastPing = Time.unscaledTime;
                if (instance != null) return;

                GameObject go = new GameObject("AutoBranchGUI");
                Object.DontDestroyOnLoad(go);
                instance = go.AddComponent<AutoBranchGUI>();
            }

            private struct Flake { public float x, y, speed, size, phase, alpha; }

            private const int FlakeCount = 70;
            private const float SnowOpacity = 0.28f;

            private Rect window = new Rect(40f, 40f, 800f, 400f);
            private bool open = false;
            private bool draggingWindow;
            private Vector2 dragOffset;
            private int activeSlider = -1;
            private int tab;
            private float nextSave;

            private Flake[] flakes;
            private Texture2D pixel, glow;
            private GUIStyle title, label, value, small, button, note;
            private bool stylesReady;

            private static readonly Color Panel = new Color(0.05f, 0.07f, 0.11f, 0.92f);
            private static readonly Color Accent = new Color(0.55f, 0.82f, 1f, 1f);
            private static readonly Color Track = new Color(1f, 1f, 1f, 0.10f);

            private void Awake()
            {
                pixel = new Texture2D(1, 1);
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();

                const int s = 32;
                glow = new Texture2D(s, s, TextureFormat.RGBA32, false);
                glow.filterMode = FilterMode.Bilinear;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(s / 2f - 0.5f, s / 2f - 0.5f)) / (s / 2f);
                        float a = Mathf.Clamp01(1f - d);
                        glow.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                glow.Apply();

                flakes = new Flake[FlakeCount];
                for (int i = 0; i < FlakeCount; i++)
                    flakes[i] = NewFlake(true);
            }

            private Flake NewFlake(bool randomY)
            {
                return new Flake
                {
                    x = Random.value,
                    y = randomY ? Random.value : -0.05f,
                    speed = Random.Range(0.04f, 0.14f),
                    size = Random.Range(3f, 8f),
                    phase = Random.Range(0f, 6.28f),
                    alpha = Random.Range(0.4f, 1f)
                };
            }

            private void OnApplicationQuit() => SaveSettings();

            private void Update()
            {
                if (Time.unscaledTime >= nextSave)
                {
                    nextSave = Time.unscaledTime + 1f;
                    if (activeSlider == -1)
                        SaveSettings();
                }

                Keyboard kb = Keyboard.current;
                if (kb != null && kb[ToggleKey].wasPressedThisFrame)
                    open = !open;
            }

            private void MakeStyles()
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
                value = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                small = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
                button = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                note = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.UpperLeft, wordWrap = true };
                stylesReady = true;
            }

            private void Fill(Rect r, Color c)
            {
                GUI.color = c;
                GUI.DrawTexture(r, pixel);
                GUI.color = Color.white;
            }

            private void DrawText(Rect r, string t, GUIStyle style, Color c)
            {
                style.normal.textColor = c;
                GUI.Label(r, t, style);
            }

            private void OnGUI()
            {
                if (Time.unscaledTime - lastPing > 0.5f || !open) return;
                if (!stylesReady) MakeStyles();

                Event e = Event.current;

                // window drag (title bar)
                Rect bar = new Rect(window.x, window.y, window.width, 46f);
                if (e.type == EventType.MouseDown && e.button == 0 && bar.Contains(e.mousePosition))
                {
                    draggingWindow = true;
                    dragOffset = e.mousePosition - window.position;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && draggingWindow)
                {
                    window.position = e.mousePosition - dragOffset;
                    window.x = Mathf.Clamp(window.x, 0f, Screen.width - window.width);
                    window.y = Mathf.Clamp(window.y, 0f, Screen.height - window.height);
                    e.Use();
                }
                else if (e.type == EventType.MouseUp)
                {
                    draggingWindow = false;
                    activeSlider = -1;
                }

                // panel
                Fill(window, new Color(Panel.r, Panel.g, Panel.b, guiOpacity));
                Fill(new Rect(window.x, window.y, window.width, 2f), Accent);

                // snowfall (low opacity, clipped to the panel)
                if (guiSnow)
                {
                    GUI.BeginClip(window);
                    if (e.type == EventType.Repaint)
                    {
                        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                        for (int i = 0; i < flakes.Length; i++)
                        {
                            Flake f = flakes[i];
                            f.y += f.speed * dt;
                            f.phase += dt * 1.2f;
                            f.x += Mathf.Sin(f.phase) * 0.012f * dt * 10f;
                            if (f.y > 1.05f) f = NewFlake(false);
                            flakes[i] = f;

                            float px = f.x * window.width;
                            float py = f.y * window.height;
                            GUI.color = new Color(1f, 1f, 1f, SnowOpacity * f.alpha);
                            GUI.DrawTexture(new Rect(px - f.size / 2f, py - f.size / 2f, f.size, f.size), glow);
                        }
                        GUI.color = Color.white;
                    }
                    GUI.EndClip();
                }

                Color faint = new Color(1f, 1f, 1f, 0.12f);
                Color dim = new Color(1f, 1f, 1f, 0.85f);
                float pad = 18f;

                // header: title on the left, status on the right
                DrawText(new Rect(window.x + pad, window.y + 6f, 300f, 28f), "Auto Branch", title, Color.white);
                DrawText(new Rect(window.x + pad, window.y + 28f, 300f, 16f), "swing & push off branches", small, new Color(1f, 1f, 1f, 0.5f));

                Fill(new Rect(window.xMax - 330f, window.y + 20f, 8f, 8f), lastOnBranch ? new Color(0.45f, 1f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.25f));
                DrawText(new Rect(window.xMax - 314f, window.y + 14f, 296f, 20f), StatusText(), small, new Color(1f, 1f, 1f, 0.65f));
                Fill(new Rect(window.x + pad, window.y + 46f, window.width - pad * 2f, 1f), faint);

                // sidebar tabs
                string[] tabNames = { "Movement", "Steering", "Arms", "Collision", "Input & GUI" };
                float sideW = 128f;
                for (int i = 0; i < tabNames.Length; i++)
                    if (Segment(new Rect(window.x + pad, window.y + 60f + i * 40f, sideW, 34f), tabNames[i], tab == i))
                        tab = i;
                Fill(new Rect(window.x + pad + sideW + 12f, window.y + 58f, 1f, window.height - 100f), faint);

                // reset button (bottom of the sidebar)
                Rect reset = new Rect(window.x + pad, window.yMax - 74f, sideW, 30f);
                bool hover = reset.Contains(e.mousePosition);
                Fill(reset, hover ? new Color(0.55f, 0.82f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.08f));
                DrawText(reset, "Reset to defaults", button, Color.white);
                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    branchActivateDist = 5f;
                    branchSpeed = 10f;
                    branchReach = 0.3f;
                    branchGrabDist = 1.1f;
                    branchResponse = 9f;
                    branchGripHold = 0.6f;
                    branchSmoothing = 0.6f;
                    branchSwingCurve = 0.75f;
                    branchSwingRadius = 1.5f;
                    branchStraight = false;
                    branchPreferFront = false;
                    branchReleaseBrake = 1f;
                    branchArmDrag = 0.5f;
                    branchHandStyle = 0;
                    branchSteerStick = 0;
                    branchInput = (int)BranchInput.RightGrip;
                    branchToggleMode = false;
                    branchNoClip = true;
                    branchCrashDisable = true;
                    branchHitMargin = 0.05f;
                    branchHeadSnap = 0.7f;
                    branchSpeedCap = 0f;
                    branchVertical = 1f;
                    branchDeadzone = 0.15f;
                    branchStickCurve = 1f;
                    branchApproachAuth = 0.6f;
                    branchGapGrace = 0.25f;
                    branchHandSpacing = 0.6f;
                    branchRegrab = 1f;
                    branchReleaseStretch = 1.25f;
                    branchHandOverHand = true;
                    branchReleaseTime = 0.6f;
                    guiSnow = true;
                    guiOpacity = 0.92f;
                    branchAntiStick = true;
                    branchStickiness = 0.33f;
                    branchHeadAuto = false;
                    ResetActivation();
                    e.Use();
                }

                // content area: two columns of settings
                float cx0 = window.x + pad + sideW + 28f;
                float gap = 24f;
                float colW = (window.xMax - pad - cx0 - gap) / 2f;
                float top = window.y + 62f;
                float rowH = 54f;
                float lw = 100f;

                Rect Cell(int col, int row, float h = 44f) => new Rect(cx0 + col * (colW + gap), top + row * rowH, colW, h);
                Rect Ctl(Rect cell) => new Rect(cell.x + lw, cell.y + 7f, cell.width - lw, 30f);
                Rect Tgl(Rect cell) => new Rect(cell.x, cell.y + 8f, cell.width, 28f);

                if (tab == 0) // Movement
                {
                    branchActivateDist = Slider(0, Cell(0, 0), "Activation Distance", branchActivateDist, 0.5f, 15f, "m");
                    branchSpeed = Slider(1, Cell(0, 1), "Speed", branchSpeed, 1f, 40f, "");
                    branchResponse = Slider(7, Cell(0, 2), "Swing Responsiveness", branchResponse, 1f, 30f, "");
                    branchGripHold = Slider(8, Cell(0, 3), "Grip Hold", branchGripHold, 0f, 1f, "", "0.00");
                    branchSwingCurve = Slider(2, Cell(0, 4), "Swing Curve", branchSwingCurve, 0f, 2f, "", "0.00");

                    branchSwingRadius = Slider(3, Cell(1, 0), "Swing Radius", branchSwingRadius, 0.5f, 4f, "m");
                    branchSpeedCap = Slider(11, Cell(1, 1), "Speed Cap (0 = off)", branchSpeedCap, 0f, 60f, "");
                    branchVertical = Slider(12, Cell(1, 2), "Vertical Strength", branchVertical, 0.25f, 2f, "x", "0.00");
                    branchStraight = Toggle(Tgl(Cell(1, 3)), "Straight mode (no swing)", branchStraight);
                }
                else if (tab == 1) // Steering
                {
                    branchHeadSnap = Slider(13, Cell(0, 0), "Head Follow (snappiness)", branchHeadSnap, 0f, 1f, "", "0.00");
                    branchDeadzone = Slider(14, Cell(0, 1), "Stick Deadzone", branchDeadzone, 0.05f, 0.5f, "", "0.00");
                    branchStickCurve = Slider(15, Cell(0, 2), "Stick Curve", branchStickCurve, 0.5f, 2.5f, "", "0.00");
                    branchApproachAuth = Slider(16, Cell(0, 3), "Approach Steering", branchApproachAuth, 0.2f, 1f, "", "0.00");

                    // steering mode: stick OR head auto-forward, never both
                    Rect mdc = Cell(1, 0);
                    DrawText(new Rect(mdc.x, mdc.y + 7f, lw, 30f), "Steering", label, dim);
                    Rect mdr = Ctl(mdc);
                    float mdh = (mdr.width - 8f) / 2f;
                    if (Segment(new Rect(mdr.x, mdr.y, mdh, 30f), "Stick", !branchHeadAuto)) branchHeadAuto = false;
                    if (Segment(new Rect(mdr.x + mdh + 8f, mdr.y, mdh, 30f), "Head Auto", branchHeadAuto)) branchHeadAuto = true;

                    Rect sc = Cell(1, 1);
                    Color stickCol = branchHeadAuto ? new Color(1f, 1f, 1f, 0.3f) : dim;
                    DrawText(new Rect(sc.x, sc.y + 7f, lw, 30f), "Steer Stick", label, stickCol);
                    Rect sr = Ctl(sc);
                    if (branchHeadAuto)
                    {
                        DrawText(sr, "not used in Head Auto", small, new Color(1f, 1f, 1f, 0.3f));
                    }
                    else
                    {
                        float sh = (sr.width - 8f) / 2f;
                        if (Segment(new Rect(sr.x, sr.y, sh, 30f), "Left", branchSteerStick == 0)) branchSteerStick = 0;
                        if (Segment(new Rect(sr.x + sh + 8f, sr.y, sh, 30f), "Right", branchSteerStick == 1)) branchSteerStick = 1;
                    }

                    branchGapGrace = Slider(17, Cell(1, 2), "Branch Gap Grace", branchGapGrace, 0f, 1f, "s", "0.00");
                    branchAntiStick = Toggle(Tgl(Cell(1, 3)), "Let go of branches (anti-stick)", branchAntiStick);
                    branchStickiness = Slider(24, Cell(1, 4), "Stickiness", branchStickiness, 0f, 1f, "", "0.00");
                }
                else if (tab == 2) // Arms
                {
                    branchReach = Slider(4, Cell(0, 0), "Reach Speed", branchReach, 0.05f, 1f, "", "0.00");
                    branchGrabDist = Slider(9, Cell(0, 1), "Grab Distance (arm length)", branchGrabDist, 0.4f, 2.5f, "m", "0.00");
                    branchSmoothing = Slider(10, Cell(0, 2), "Smoothing", branchSmoothing, 0f, 1f, "", "0.00");
                    branchArmDrag = Slider(6, Cell(0, 3), "Arm Drag (idle)", branchArmDrag, 0f, 1f, "", "0.00");
                    branchHandSpacing = Slider(18, Cell(0, 4), "Hand Spacing", branchHandSpacing, 0.1f, 2f, "m", "0.00");

                    Rect hc = Cell(1, 0);
                    DrawText(new Rect(hc.x, hc.y + 7f, lw, 30f), "Hand Style", label, dim);
                    if (Selector(Ctl(hc), Movement.BranchHandStyleNames[branchHandStyle], out int handStep))
                        branchHandStyle = (branchHandStyle + handStep + Movement.BranchHandStyleNames.Length) % Movement.BranchHandStyleNames.Length;

                    branchRegrab = Slider(19, Cell(1, 1), "Regrab Cooldown", branchRegrab, 0f, 3f, "s", "0.00");
                    branchReleaseStretch = Slider(20, Cell(1, 2), "Release Stretch", branchReleaseStretch, 1.05f, 2f, "x", "0.00");
                    branchPreferFront = Toggle(Tgl(Cell(1, 3)), "Arms swing from the front", branchPreferFront);
                    branchHandOverHand = Toggle(Tgl(Cell(1, 4)), "Hand over hand", branchHandOverHand);
                }
                else if (tab == 3) // Collision
                {
                    branchNoClip = Toggle(Tgl(Cell(0, 0)), "No Clip (pass through trees)", branchNoClip);
                    branchCrashDisable = Toggle(Tgl(Cell(0, 1)), "Hitting a tree turns it off", branchCrashDisable);
                    branchHitMargin = Slider(21, Cell(0, 2), "Tree Hit Margin", branchHitMargin, 0f, 0.3f, "m", "0.00");
                    DrawText(Cell(0, 3, 60f), "With No Clip off you collide with trees. Touching one with your body turns Auto Branch off until you activate it again.", note, new Color(1f, 1f, 1f, 0.5f));

                    branchReleaseBrake = Slider(5, Cell(1, 0), "Release Easing", branchReleaseBrake, 0f, 1f, "", "0.00");
                    branchReleaseTime = Slider(22, Cell(1, 1), "Release Duration", branchReleaseTime, 0.1f, 2f, "s", "0.00");
                }
                else // Input & GUI
                {
                    Rect ic = Cell(0, 0);
                    DrawText(new Rect(ic.x, ic.y + 7f, lw, 30f), "Input", label, dim);
                    if (Selector(Ctl(ic), Movement.BranchInputNames[branchInput], out int step))
                    {
                        branchInput = (branchInput + step + Movement.BranchInputNames.Length) % Movement.BranchInputNames.Length;
                        ResetActivation();
                    }

                    Rect mc = Cell(0, 1);
                    DrawText(new Rect(mc.x, mc.y + 7f, lw, 30f), "Mode", label, dim);
                    Rect mr = Ctl(mc);
                    float half = (mr.width - 8f) / 2f;
                    if (Segment(new Rect(mr.x, mr.y, half, 30f), "Hold", !branchToggleMode) && branchToggleMode)
                    {
                        branchToggleMode = false;
                        ResetActivation();
                    }
                    if (Segment(new Rect(mr.x + half + 8f, mr.y, half, 30f), "Toggle", branchToggleMode) && !branchToggleMode)
                    {
                        branchToggleMode = true;
                        ResetActivation();
                    }

                    guiSnow = Toggle(Tgl(Cell(1, 0)), "Snowfall", guiSnow);
                    guiOpacity = Slider(23, Cell(1, 1), "Panel Opacity", guiOpacity, 0.4f, 1f, "", "0.00");
                }

                DrawText(new Rect(window.x + pad, window.yMax - 28f, window.width - pad * 2f, 20f), ToggleKey + " to hide  -  drag title bar to move  -  settings auto-save", small, new Color(1f, 1f, 1f, 0.35f));
            }

            // Pill switch. Returns the new value
            private bool Toggle(Rect r, string text, bool on)
            {
                Event e = Event.current;
                bool hover = r.Contains(e.mousePosition);

                DrawText(new Rect(r.x, r.y, r.width - 50f, r.height), text, label, hover ? Color.white : new Color(1f, 1f, 1f, 0.85f));

                Rect pill = new Rect(r.xMax - 44f, r.y + 4f, 40f, 20f);
                Fill(pill, on ? new Color(0.55f, 0.82f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.12f));
                Fill(new Rect(on ? pill.xMax - 18f : pill.x + 2f, pill.y + 2f, 16f, 16f), Color.white);

                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    e.Use();
                    return !on;
                }
                return on;
            }

            private string StatusText()
            {
                if (lastOnBranch) return ArmStatus.Length > 0 ? ArmStatus : "On branch";
                if (Time.time < crashStatusUntil) return "Hit a tree - activate again";

                string input = Movement.BranchInputNames[branchInput];
                if (branchToggleMode)
                    return branchToggled ? "Armed - " + input + " to turn off" : input + " to turn on";
                return "Hold " + input + " near a branch";
            }

            // ‹ value › picker. Returns true on click with step = -1 / +1
            private bool Selector(Rect r, string text, out int step)
            {
                Event e = Event.current;
                step = 0;

                float arrow = 30f;
                Rect left = new Rect(r.x, r.y, arrow, r.height);
                Rect right = new Rect(r.xMax - arrow, r.y, arrow, r.height);

                Fill(r, new Color(1f, 1f, 1f, 0.08f));
                Fill(left, left.Contains(e.mousePosition) ? new Color(0.55f, 0.82f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.06f));
                Fill(right, right.Contains(e.mousePosition) ? new Color(0.55f, 0.82f, 1f, 0.28f) : new Color(1f, 1f, 1f, 0.06f));

                DrawText(left, "<", button, Color.white);
                DrawText(right, ">", button, Color.white);
                DrawText(new Rect(r.x + arrow, r.y, r.width - arrow * 2f, r.height), text, button, Accent);

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (left.Contains(e.mousePosition)) { step = -1; e.Use(); return true; }
                    if (right.Contains(e.mousePosition)) { step = 1; e.Use(); return true; }
                }
                return false;
            }

            // Two-option segmented button. Returns true when clicked
            private bool Segment(Rect r, string text, bool selected)
            {
                Event e = Event.current;
                bool hover = r.Contains(e.mousePosition);

                Fill(r, selected ? new Color(0.55f, 0.82f, 1f, 0.35f) : hover ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.07f));
                DrawText(r, text, button, selected ? Color.white : new Color(1f, 1f, 1f, 0.65f));

                if (e.type == EventType.MouseDown && e.button == 0 && hover)
                {
                    e.Use();
                    return true;
                }
                return false;
            }

            private float Slider(int id, Rect r, string name, float val, float min, float max, string unit, string fmt = "0.0")
            {
                Event e = Event.current;

                DrawText(new Rect(r.x, r.y, r.width, 20f), name, label, new Color(1f, 1f, 1f, 0.85f));
                DrawText(new Rect(r.x, r.y, r.width, 20f), val.ToString(fmt) + unit, value, Accent);

                Rect track = new Rect(r.x, r.y + 28f, r.width, 6f);
                Rect hit = new Rect(r.x - 6f, r.y + 20f, r.width + 12f, 22f);

                if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                {
                    activeSlider = id;
                    draggingWindow = false;
                    e.Use();
                }

                if (activeSlider == id && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                {
                    val = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                    e.Use();
                }

                float t = Mathf.InverseLerp(min, max, val);
                Fill(track, Track);
                Fill(new Rect(track.x, track.y, track.width * t, track.height), Accent);

                float knob = activeSlider == id ? 16f : 13f;
                Fill(new Rect(track.x + track.width * t - knob / 2f, track.y + track.height / 2f - knob / 2f, knob, knob), Color.white);

                return val;
            }
        }
    }
}

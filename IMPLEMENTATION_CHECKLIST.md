# CSE 165 Project 2 Implementation Checklist

## Required Implementation

### 1. Race Track

- Machu Picchu model is imported from `Assets/MachuPicchu/machu_picchu_2.obj`.
- Model scale is `1 / 39.37`, converting the downloaded inch units to Unity meters.
- Mesh colliders are added to all Machu Picchu mesh children.
- Drone uses a kinematic Rigidbody and trigger SphereCollider for terrain collision.
- XYZ checkpoints are parsed as whitespace-separated inch coordinates and converted to meters.
- Runtime accepts up to 100 checkpoints.
- Checkpoint reach radius is fixed at 30 feet, `9.144m`.
- Grading track can be imported with `CSE165 Project 2 > Import Competition XYZ...`.
- Track load priority:
  1. `Application.persistentDataPath/competition.xyz`
  2. `Assets/StreamingAssets/competition.xyz`
  3. visible scene checkpoints
  4. fallback sample track

### 2. Flight Controls

- Quest build flight uses hand tracking only.
- Both hands must be tracked, and the left hand must be closed as a fist to move.
- Opening the left hand or losing either hand stops the drone immediately.
- Travel direction is the tracked-space vector from the left hand to the right hand, transformed through the drone tracking root.
- With the right hand closed as a fist, travel speed scales with hand separation.
- With the right hand open/flat, travel speed is a constant 70 m/s boost.
- Head orientation is not used for travel direction, so looking around does not steer.
- No autopilot, target seeking, or collision avoidance is implemented.
- Movement is continuous with configurable acceleration/deceleration.
- Editor keyboard controls are compiled only under `UNITY_EDITOR`.

### 3. Wayfinding And Motion Sickness Mitigation

- World-coordinate wayfinding: line from drone to target and a world arrow near the drone.
- Head-coordinate wayfinding: HUD target arrow and distance display.
- Wayfinding aids are in separate coordinate systems.
- First-person view is the default Quest mode.
- Cockpit view displays a virtual cockpit around the pilot.
- Third-person view places the camera behind the drone and displays the drone.
- Holding both thumb-index pinches cycles view modes using hand tracking.

### 4. Gameplay

- Race starts at checkpoint 1.
- Editor starts facing checkpoint 2; Quest starts level with neutral yaw to avoid forced spinning.
- Countdown blocks movement before start.
- Stopwatch starts when countdown ends and remains visible.
- Checkpoints must be reached in order.
- Current, pending, and cleared checkpoints have different visual states.
- Finish stops controls and leaves final time visible.
- Finished state shows restart instructions; holding both hands flat/open restarts the race.
- Restart uses a 5-second countdown with controls locked and the stopwatch reset to 0.
- Terrain collision resets to the last cleared checkpoint.
- Crash recovery countdown is always at least 3 seconds and is shown as the crash penalty.
- Lap timer continues during crash recovery, so the recovery lockout is included in the final time.

## Extra Credit Implemented

The assignment caps implementation extra credit at 10 points. The code implements more than 10 possible points; the safest max-credit combination is Audio + Spatialized Audio + Track Editor, or Audio + Spatialized Audio + Ghost Champion.

### Track Editor - 5 points

- Right thumb-ring toggles edit mode.
- Entering edit mode starts a new draft track at the drone's current position.
- Right thumb-middle adds checkpoints.
- Left thumb-middle saves the edited track to persistent storage.
- Left thumb-ring cycles through saved tracks and starts a normal race on the selected track.
- Saved tracks use the same XYZ inch-coordinate format as grading files.

### Audio Effects - 2 points

- Procedural motor loop pitch and volume respond to speed.
- Countdown, checkpoint, crash, and finish effects are generated and played.

### Spatialized Audio Wayfinding - 3 points

- Next waypoint emits a spatialized looping pulse.
- Left thumb-little toggles audio-only wayfinding.
- Audio-only mode disables world line, world arrow, HUD target arrow, and HUD distance.
- Checkpoint sphere display remains visible, as allowed by the writeup.

### Ghost Champion - 5 points

- Each run records position and orientation at fixed 90 Hz sample times.
- Missed frame samples are interpolated to preserve the 90 Hz timeline.
- The best run is saved to persistent storage.
- Later races show a translucent, non-symmetric ghost drone replaying the best run.
- Right thumb-little toggles ghost display.

## Non-Code Deliverables Still Required

- Project Design Document with at least 3 travel storyboards and 3 wayfinding storyboards.
- Heuristic evaluation section in the design document.
- Video showing all required features and selected extra-credit features.
- Public GitHub repository link.

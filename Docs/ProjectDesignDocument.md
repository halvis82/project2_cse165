# CSE 165 Project 2 Design Document

## Project Summary

This project implements a Meta Quest hand-tracked drone navigation trainer at Machu Picchu. The operator flies through ordered checkpoints using hand tracking only, with visual and audio wayfinding, crash recovery, motion-sickness mitigation views, and extra-credit systems for track editing, audio-only waypoint navigation, and ghost champion racing.

## Selected Interaction Design

The final travel technique uses a two-fist relative vector: both hands must be tracked and closed, and the vector from the left fist to the right fist becomes the flight direction. Opening either hand, losing tracking, or holding the fists too close together stops the drone immediately. The user can look around independently because head orientation is not used for steering.

The final wayfinding technique combines world-space aids and head-space aids. In normal mode, the user sees checkpoint spheres, a world line/arrow toward the next checkpoint, and HUD target distance/heading. In audio-only extra-credit mode, visual wayfinding is disabled except checkpoint display, and a spatialized pulse emits from the active waypoint.

## Travel Storyboards

![Travel and wayfinding storyboards](storyboards/project2_storyboards.svg)

### Travel 1: Two-Fist Relative Flight

The user closes both hands into fists. The left fist acts as an anchor, and the right fist's position relative to it controls direction: right fist left of the left fist moves left, right fist forward of the left fist moves forward, and vertical offsets climb or descend. Opening either hand stops immediately. This was selected because it satisfies the no-head-steering requirement and prevents drift when tracking is uncertain.

### Travel 2: Point-and-Pinch Flight

The user points the right hand toward the desired travel direction and pinches with the left hand to move. This was viable but rejected because it can become ambiguous if the hand aim pose feels coupled to where the user is looking.

### Travel 3: Palm Tilt Joystick

The user tilts one palm as an invisible joystick and pinches to confirm motion. This was viable but rejected because tilt can be ambiguous when the user turns their body or reaches around in VR.

## Wayfinding Storyboards

### Wayfinding 1: World-Space Checkpoint Line

A world-space line and arrow connect the drone to the next checkpoint. This was selected because it remains grounded in the environment and makes distant checkpoints visible.

### Wayfinding 2: Head-Space HUD Arrow

The HUD displays current checkpoint number, distance, and heading arrow. This was selected because it stays readable while the user looks around and uses a different coordinate system from the world-space line.

### Wayfinding 3: Spatial Audio Pulse

The active waypoint emits a spatialized pulse. This was selected as extra credit and as an alternate wayfinding condition when visual aids are disabled.

## Heuristic Evaluation

| Heuristic | Violation and rationale | Severity | Recommendation |
| --- | --- | --- | --- |
| Visibility of system status | Mostly satisfied. Countdown, timer, speed, checkpoint index, status text, and view mode are visible. | 0 | Keep status messages short so they do not distract during racing. |
| Match between system and real world | Mostly satisfied. Two-handed steering and spatial waypoint audio map naturally to drone piloting and navigation. | 0 | In demo narration, explicitly describe the left fist as the anchor and the right fist as the direction handle. |
| User control and freedom | Mostly satisfied. User can stop by opening either hand, switch views, toggle audio-only mode, and reset through crash recovery. | 1 | Add an explicit restart gesture in a future version if time allows. |
| Consistency and standards | Mostly satisfied. Checkpoints use stable colors and ordered numbering; HUD uses conventional timer/speed displays. | 0 | Keep all demo terminology consistent: checkpoint, waypoint, race, crash reset. |
| Error prevention | Partially satisfied. No autopilot or collision avoidance is allowed, but crash recovery prevents unrecoverable states. | 1 | Keep visible checkpoint spheres enabled even in audio-only mode, as allowed by the writeup. |
| Recognition rather than recall | Mostly satisfied. HUD and world arrows reduce memory burden; status text names editor/test actions. | 1 | For final demo, show the gesture list briefly before headset footage. |
| Flexibility and efficiency of use | Satisfied. Editor keyboard fallback supports development only; Quest uses hand gestures. Track editor and saved tracks support advanced users. | 0 | Avoid relying on keyboard fallback in submitted demo. |
| Aesthetic and minimalist design | Mostly satisfied. HUD is compact and wayfinding aids are simple. | 0 | If visual clutter appears in video, use audio-only mode to demonstrate alternate navigation. |
| Help users recognize, diagnose, recover from errors | Mostly satisfied. Crash status and countdown make recovery obvious; invalid tracks log errors/warnings. | 1 | In demo, intentionally crash once to show recovery behavior. |
| Help and documentation | Partially satisfied. Implementation checklist and this document explain features. | 1 | Include the public GitHub URL and demo video URL in the final submission message. |

## Implementation Checklist

The implementation checklist is maintained in `IMPLEMENTATION_CHECKLIST.md`. It maps the assignment requirements to code features and identifies selected extra-credit features.

## Submission Links

- Public GitHub repository: https://github.com/halvis82/project2_cse165
- Built APK artifact, local workspace: `Builds/Project2Race.apk`
- Demo video: record and attach/upload before final submission.

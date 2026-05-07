# CSE 165 Project 2 Design Document

## Project Summary

This project implements a Meta Quest hand-tracked drone navigation trainer at Machu Picchu. The operator flies through ordered checkpoints using hand tracking only, with visual and audio wayfinding, crash recovery, motion-sickness mitigation views, and extra-credit systems for track editing, audio-only waypoint navigation, and ghost champion racing.

## Selected Interaction Design

The final travel technique uses left-hand thumb-index pinch as analog throttle and right-hand aim direction as the flight vector. The user can look around independently because head orientation is not used for steering. This keeps travel continuous, avoids autopilot, and makes the mapping close to pointing a real drone in the intended direction.

The final wayfinding technique combines world-space aids and head-space aids. In normal mode, the user sees checkpoint spheres, a world line/arrow toward the next checkpoint, and HUD target distance/heading. In audio-only extra-credit mode, visual wayfinding is disabled except checkpoint display, and a spatialized pulse emits from the active waypoint.

## Travel Storyboards

![Travel and wayfinding storyboards](storyboards/project2_storyboards.svg)

### Travel 1: Point-and-Pinch Flight

The user points the right hand toward the desired travel direction and pinches with the left hand to move. Releasing the pinch decelerates to a stop. This was selected because it is direct, fast to learn, and satisfies the no-head-steering requirement.

### Travel 2: Two-Hand Throttle Lever

The user holds both hands as if gripping a virtual throttle bar; hand separation controls speed and the bar direction controls flight. This was viable but rejected because it occupies both hands continuously and makes view-switching or editing gestures harder.

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
| Match between system and real world | Mostly satisfied. Pointing to fly and spatial waypoint audio map naturally to drone piloting and navigation. | 0 | In demo narration, explicitly describe left pinch as throttle and right aim as direction. |
| User control and freedom | Mostly satisfied. User can stop by releasing throttle, switch views, toggle audio-only mode, and reset through crash recovery. | 1 | Add an explicit restart gesture in a future version if time allows. |
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

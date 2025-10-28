NetworkARv2

A networked AR application with co-location support, developed for the user study of the Meme Sharing Project.

🧠 Overview

NetworkARv2 enables multiple AR headsets to interact in a shared augmented environment. Designed for meme-sharing user studies, it supports synchronized AR memes (appearing, location, position, animation) across devices with real-time communication.

🧩 Unity Version

Unity 6 is required

🧩 Device Requirements

Meta Quest 3, Meta Quest 3s, or Meta Quest Pro

🚀 How to Run

Network Setup

- The application requires at least three AR headsets.
- One device acts as the network host, while the others connect as clients.
- All devices must be connected to the same Wi-Fi network.
  

Build the Application

- Open the Unity project and build the sample scene located at: Assets/Scenes/SampleScene
- Deploy the build to each headset.
  

Start the Application

- Step 1: Launch the application on the host device first. When prompted, set the PlayerID: Host device: PlayerID = 0. After entering the PlayerID, click Enter to join the network.
- Step 2: Wait about 5 seconds, then start the application on the client devices. When prompted, set the PlayerID:  Client devices: PlayerID = 1–24, Each PlayerID corresponds to a unique set of 3 memes, as used in my prior user study. After entering the PlayerID, click Enter to join the network.



Usage Notes

- Keep the host device unworn — it functions solely as the network host.
- The client devices should be worn by participants for chatting and meme-sharing interactions.

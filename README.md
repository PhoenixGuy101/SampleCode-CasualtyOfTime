# SampleCode-CasualtyOfTime
Some sample code from Casualty Of Time, 2D platformer created for My First Game Jam. Full code under Jumpman-Blockbreak repository.

The sample code is just the player controller script. Most of the Platformer Template (originally devised in Animus Core) is consolidated into this file, which relies heavily on 1d Kinematics and other vector principles to effectively control the player movement variables.
Player initial jump velocity is derived from the Gamemanager, as the Designer simply needs to input the jump height and time to get the proper force of gravity and the initial jump velocity from the Gamemanager.

# Tectonic-Order-Generation-Tool

At the very early stages of development on my 4X grand strategy game called Hex Imperium, I ran into the problem of the random map generation. How do you go about creating a world that fits your needs and can be easily balanced? How curated should the map generation be?

I decided to let nature do most of the work for me and review the results. The algorithm I am using tries to simulate the Earth's plate tectonics, but on a very high level. I in no way have any background on geology, and gather all information I need from wikipedia and random gifs on the internet, so if you are looking to find a sophisticated simulation of the Earth's tectonic plates, this is not the place.

This repo is an adventure of personal interest and is not guarenteed to produce positive results.

I am using the Unity game engine for Hex Imperium and this tool application.

# Current State

FEATURES
- Plate initialization.
- Plate interaction (done in steps).
- Plate/Geographical/Height map layers are available.
- Direction vectors for every hextile that show the movement of the hextile for the next step.

GENERATION OPTIONS
- Seed that can produce unique results.
- Map size drop down (crashes for any value except the first).
- Water level dropdown (not functioning yet).
- Climate dropdown (not functioning yet).
- Resources dropdown (not functioning yet).
- Distribution dropdown (not functioning yet).

TODO
- The height of each plate/hextile is as of now picked randomly. This will change to a more continuous approach.
- A slightly more complex interaction between plates/hextiles.
- Geographical generation (forests, mountains, deserts, etc...) - This will take some time.

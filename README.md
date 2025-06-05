# orogeny
Plate tectonics simulator

The goal is not a game, exactly, nor a scientific simulation, but more of a chill experience. Watch a toy planet evolve over billions of years as plates rift apart, drift, and collide again with each other in a generally plausible fashion.

"Game" play:
Play/pause is in the upper right, and works as you would expect.
Under it are three lists of checkboxes, letting you toggle certain game aspects on and off. (Currently these are aimed mostly at development.)
    The first are subsystems such as plate movement and volcanism.
    The second are event types which will automatically pause the game for debugging purposes.
    The third are different visualizations. Of these, only "Terrain" would be on in normal usage.
Top left shows current frames per second.
Bottom left will show your current latitude/longitude, and has a reset button to move the viewpart back to its default position.
In the lower right is a map of the entire planet, for overview and navigation purposes. Parts of the globe not currently visible are shown darkened. The dropdown lets your change between several different projections, though Natural Earth (a Robinson clone) is likely the best.
Rotate the planet with arrows, and zoom in and out with with mouse scroll wheel. Shift will make these movements bigger, while control will make them smaller.

TODO:
    Fix MORBing (new plate creation at divergent boundaries)
    Add rifting
    Create randomized starting configurations
    Make mantle convection more random
    Add a more realistic "slab pull" element to the mantle convection model currently being used

Matthew Dockrey
orogeny@attoparsec.com
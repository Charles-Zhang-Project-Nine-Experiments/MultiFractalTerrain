# Multi-fracal Generator

Using very simple perlin noise to allow custom testing and implementation of varying terrain-related generation methods.
Depends on FastNoiseLite to provide fast Perlin noise implementation.
At the moment due to the limit of underlying library we suffer from non-linear units when it comes to specify perlin noise scale.

v0.1 (20231003): Originally an experimentation with Blender PythonNet for mesh output;  
v0.2 (20231010): Added bitmap drawing output support so we can see result outside Blender environment;  
v0.3 (20231011): Made Generation into Fluent so we can easily REPL use it. Moved into proper Visual Studio C# project.
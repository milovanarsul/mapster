Mapster
-----------------------

# Final project solution 

To store map features in a more efficient way, without using strings, I put all the map features in the `GeographicalTypes` Enum.

From there on, the `Properties` Dictionary uses the declared enum as the key, inside `DataFiles.cs`. 

I then proceeded to replace all the strings used to store map information with their respective enum case inside `RenderingTypes.cs` and `TheRenderer.cs`.

-----------------------
The solution in this repository contains several projects that aim to read and extract information from the OpenStreeMap binary [`osm.pbf`](https://wiki.openstreetmap.org/wiki/PBF_Format) format, as well as interpret and present this data in a user-facing way.

To this end the solution contains several parts that are used at different times in the lifetime of the application.

## General Overview

### I. Common - Library
A common set of data structures used throughout the codebase.

### II. DataPipeline
#### 1. OSMDataParser - Library
Reads, parses and interprets data from an OpenStreetMap binary file in [PBF](https://wiki.openstreetmap.org/wiki/PBF_Format) format. It extensively uses iterators in order to extract data on an as-needed basis and optimizes for memory efficiency rather than CPU usage efficiency. That being said it supports reading per-blob data in parallel, which can speed up parsing significantly.

It is used by the MapFeatureGenerator to extract [Way](https://wiki.openstreetmap.org/wiki/Way) and [Node](https://wiki.openstreetmap.org/wiki/Node) information from a [PBF](https://wiki.openstreetmap.org/wiki/PBF_Format) file.

#### 2. MapFeatureGenerator - Executable
Uses OSMDataParser to read mapping information from a PBF file and outputs it to a format that can be used by the service to serve map data.

The main work is done in `CreateMapDataFile` that generates a binary file that can be mapped into process memory by the service making load times for the services practically instant.

This executable would be run in an automated fashion in order to synchronize with any updates to the OSM data.
In order to run the application one should provide the input `.osm.pbf` file as well as a name for the output file that will be generated.

### III. Rendering
#### 1. TileRenderer - Library
Used to tessellate and render a set of map features.

The two most important methods are the two extension methods:
  - `Tessellate` - That creates a BaseShape with unbound pixel coordinates for a map feature
  - `Render` - That takes a collection of shapes and, based on their Z index, renders them, scaled, to a RGBA image

### IV. Service
#### 1. Service - Executable
A .NET MinimalAPI application with, currently, a single endpoint `/render` that takes a bounding box and an, optional, size and renders a png image with the geographical features contained within the bounding box.

### V. Client
#### 1. ClientApplication
This is the client/user facing part of the application and it is responsible to creating and sending requests to the backend/service.
Since this project is only at POC stage it just renders a window with the tile for Andorra.

### VI. Tests
#### 1. TestCommon
Tests for reading and interpreting a DataFile.
#### 2. TestOSMDataReader
Tests for reading and parsing a `.osm.pbf` file.
#### 3. TestTileRenderer
Tests for rendering a file resulted from MapFeatureGenerator.


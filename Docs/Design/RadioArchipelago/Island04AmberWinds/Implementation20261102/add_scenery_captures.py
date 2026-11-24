from pathlib import Path
p=Path(__file__).with_name('UnityStaging')/'AmberWindsIslandJobs.cs'
s=p.read_text(encoding='utf8')
s=s.replace('''            RenderTexture texture = null; Texture2D pixels = null;
            try
''','''            RenderTexture texture = null; Texture2D pixels = null;
            IslandLivingNature nature = null; IslandRooftopMotion motion = null;
            try
''',1)
s=s.replace('''                var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
                foreach (var transform''','''                var root = (GameObject)PrefabUtility.InstantiatePrefab(definition.Prefab, scene);
                Bounds sceneryBounds = root.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(Vector3.zero, Vector3.zero),
                    (a, renderer) => { a.Encapsulate(renderer.bounds); return a; });
                nature = root.AddComponent<IslandLivingNature>(); nature.Configure(definition, route);
                nature.UpdatePresentation(route.Length * .66f, 0);
                motion = new IslandRooftopMotion(); motion.Configure(root.transform, root.transform, definition.RooftopMotionJson);
                for (int tick = 0; tick < 10; tick++) motion.Tick(.1f);
                foreach (var transform''',1)
s=s.replace('''                        Bounds bounds = root.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(Vector3.zero, Vector3.zero), (a, renderer) => { a.Encapsulate(renderer.bounds); return a; });
                        target = bounds.center + Vector3.up * 10;
                        float extent = Mathf.Max(bounds.extents.x, bounds.extents.z);''','''                        target = sceneryBounds.center + Vector3.up * 10;
                        float extent = Mathf.Max(sceneryBounds.extents.x, sceneryBounds.extents.z);''')
s=s.replace('''                        var frame = route.Evaluate(route.Length * (i - .5f) / 6);
                        eye = frame.Position''','''                        float station = route.Length * (i - .5f) / 6;
                        nature.UpdatePresentation(station, 0);
                        var frame = route.Evaluate(station);
                        eye = frame.Position''')
needle='''                    File.WriteAllBytes(folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png", pixels.EncodeToPNG());
                }
            }
            finally'''
replacement='''                    File.WriteAllBytes(folder + "/" + (i == 0 ? "overview" : "route-" + i) + ".png", pixels.EncodeToPNG());
                }
                // Art views from the actual fixed gameplay rig and portrait FOV,
                // without introducing an actor, clock, timeline or old song.
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
                texture = new RenderTexture(720, 1280, 24); texture.Create();
                pixels = new Texture2D(720, 1280, TextureFormat.RGBA32, false);
                camera.aspect = 9f / 16; camera.fieldOfView = definition.CameraFieldOfView;
                var rig = new IslandCloseCameraRig();
                foreach (float station in new[] { 172f,185f,200f,210f,244f,272f,464f,506f,542f,580f,675f })
                {
                    nature.UpdatePresentation(station, 0);
                    var pose = rig.Step(route, station, 0, definition.CameraBackDistance, definition.CameraHeight,
                        definition.CameraLookAhead, definition.CameraFocusHeight, definition.CameraHeadingLead, definition.CameraSmoothing, 0, true);
                    camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                    RenderTexture.active = texture; pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); pixels.Apply(false, false);
                    File.WriteAllBytes(folder + "/portrait-route-" + station.ToString("000", System.Globalization.CultureInfo.InvariantCulture) + ".png", pixels.EncodeToPNG());
                }
            }
            finally'''
assert needle in s
s=s.replace(needle,replacement,1)
s=s.replace('''                RenderTexture.active = previousTarget; Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear);''','''                motion?.Dispose(); nature?.Dispose();
                RenderTexture.active = previousTarget; Shader.SetGlobalVector("_RA_ClearViewCamera", previousClear);''',1)
p.write_text(s,encoding='utf8')
print('Scenery captures now include actual runtime nature, windmills and eleven portrait fixed-rig route views.')

﻿using System;
using System.Collections.Generic;
using System.Linq;
using Fusee.Base.Common;
using Fusee.Base.Core;
using Fusee.Engine.Common;
using Fusee.Engine.Core;
using Fusee.Math.Core;
using Fusee.Serialization;
using Fusee.Xene;
using static Fusee.Engine.Core.Input;
using static Fusee.Engine.Core.Time;
using static System.Math;
using Fusee.Engine.GUI;

namespace FuseeApp
{

    [FuseeApplication(Name = "Test", Description = "Yet another FUSEE App.")]
    public class Test : RenderCanvas
    {
        // Horizontal and vertical rotation Angles for the displayed object 
        private static float _angleHorz = M.PiOver4, _angleVert, _distance;

        // Horizontal and vertical angular speed
        private static float _angleVelHorz, _angleVelVert, _distanceVel;

        // Overall speed factor. Change this to adjust how fast the rotation reacts to input
        private const float RotationSpeed = 7;

        // Damping factor 
        private const float Damping = 0.8f;

        private SceneContainer _scene;
        private SceneRenderer _sceneRenderer;
        private ScenePicker _scenePicker;
        private PickResult _currentPick;
        private float3 _oldColor;
        private TransformComponent _lowerAxleTransform;
        private TransformComponent _middleAxleTransform;
        private TransformComponent _upperAxleTransform;
        private TransformComponent _footTransform;
        private TransformComponent _pointer;
        private TransformComponent _rightPincerTransform;
        private TransformComponent _leftPincerTransform;
        private float3 _virtualPos;
        private bool _open;
        private bool _move;

        // Init is called on startup. 
        public override void Init()
        {
            // Set the clear color for the backbuffer to white (100% intensity in all color channels R, G, B, A).
            RC.ClearColor = new float4(1, 1, 1, 1);

            // Load the model
            _scene = AssetStorage.Get<SceneContainer>("roboter_arm.fus");

            //Set Transforms for the Axles
            _lowerAxleTransform = _scene.Children.FindNodes(node => node.Name == "LowerAxle")?.FirstOrDefault()?.GetTransform();
            _middleAxleTransform = _scene.Children.FindNodes(node => node.Name == "MiddleAxle")?.FirstOrDefault()?.GetTransform();
            _upperAxleTransform = _scene.Children.FindNodes(node => node.Name == "UpperAxle")?.FirstOrDefault()?.GetTransform();
            _footTransform = _scene.Children.FindNodes(node => node.Name == "Foot")?.FirstOrDefault()?.GetTransform();

            _rightPincerTransform = _scene.Children.FindNodes(node => node.Name == "RightLowerAxle")?.FirstOrDefault()?.GetTransform();
            _leftPincerTransform = _scene.Children.FindNodes(node => node.Name == "LeftLowerAxle")?.FirstOrDefault()?.GetTransform();


            _pointer = _scene.Children.FindNodes(node => node.Name == "Pointer")?.FirstOrDefault()?.GetTransform();

            //Set Rotations to 0
            _lowerAxleTransform.Rotation = new float3(0, 0, 0);
            _middleAxleTransform.Rotation = new float3(0, 0, 0);
            _upperAxleTransform.Rotation = new float3(0, 0, 0);

            _virtualPos = new float3(0, 5, 0); //at the position of the upper axle

            _open = false;

            // Wrap a SceneRenderer around the model.
            _sceneRenderer = new SceneRenderer(_scene);
            _scenePicker = new ScenePicker(_scene);
        }

        // RenderAFrame is called once a frame
        public override void RenderAFrame()
        {

            // Clear the backbuffer
            RC.Clear(ClearFlags.Color | ClearFlags.Depth);

            // Mouse and keyboard movement
            if (Mouse.MiddleButton)
            {
                _angleVelHorz = -RotationSpeed * Mouse.XVel * DeltaTime * 0.0005f;
                _angleVelVert = -RotationSpeed * Mouse.YVel * DeltaTime * 0.0005f;
            }
            else if (Mouse.WheelVel != 0)
            {
                _distanceVel += RotationSpeed * Mouse.WheelVel * DeltaTime * 0.0005f;
            }
            else
            {
                var curDamp = (float)System.Math.Exp(-Damping * DeltaTime);
                _angleVelHorz *= curDamp;
                _angleVelVert *= curDamp;
                _distanceVel *= curDamp;
            }


            _angleHorz += _angleVelHorz;
            _angleVert += _angleVelVert;
            _distance += _distanceVel;

            // Create the camera matrix and set it as the current ModelView transformation
            var mtxRot = float4x4.CreateRotationX(_angleVert) * float4x4.CreateRotationY(_angleHorz);
            var mtxCam = float4x4.LookAt(0, 2, -20 + _distance, 0, 1, 0, 0, 1, 0);
            RC.View = mtxCam * mtxRot;

            //Pick Parts
            if (Mouse.LeftButton)
            {
                float2 pickPosClip = Mouse.Position * new float2(2.0f / Width, -2.0f / Height) + new float2(-1, 1);
                _scenePicker.View = RC.View;
                _scenePicker.Projection = RC.Projection;

                List<PickResult> pickResults = _scenePicker.Pick(pickPosClip).ToList();
                PickResult newPick = null;
                if (pickResults.Count > 0)
                {
                    pickResults.Sort((a, b) => Sign(a.ClipPos.z - b.ClipPos.z));
                    newPick = pickResults[0];
                }

                if (newPick?.Node != _currentPick?.Node)
                {
                    if (_currentPick != null)
                    {
                        ShaderEffectComponent shaderEffectComponent = _currentPick.Node.GetComponent<ShaderEffectComponent>();
                        shaderEffectComponent.Effect.SetEffectParam("DiffuseColor", _oldColor);
                    }
                    if (newPick != null)
                    {
                        ShaderEffectComponent shaderEffectComponent = newPick.Node.GetComponent<ShaderEffectComponent>();
                        _oldColor = (float3)shaderEffectComponent.Effect.GetEffectParam("DiffuseColor");
                        shaderEffectComponent.Effect.SetEffectParam("DiffuseColor", new float3(1, 0.4f, 0.4f));
                    }
                    _currentPick = newPick;
                }
            }


            //Rotate Picked Part
            if (_currentPick != null)
            {
                TransformComponent transformComponent = _currentPick.Node.GetTransform();

                transformComponent.Rotation.x = transformComponent.Rotation.x + Keyboard.UpDownAxis * Time.DeltaTime;
                transformComponent.Rotation.y = transformComponent.Rotation.y + Keyboard.LeftRightAxis * Time.DeltaTime;
                transformComponent.Rotation.z = transformComponent.Rotation.z + Keyboard.WSAxis * Time.DeltaTime;

                if (Keyboard.GetButton(96))
                {
                    transformComponent.Rotation = new float3(0, 0, 0);
                }
                Diagnostics.Log(transformComponent.Rotation);
            }

            //Inverse Kinematics
            if (_currentPick == null)
            {
                _virtualPos += new float3(Keyboard.LeftRightAxis * Time.DeltaTime, Keyboard.WSAxis * Time.DeltaTime, Keyboard.UpDownAxis * Time.DeltaTime);

                _pointer.Translation = _virtualPos;

                double tempDist = Math.Sqrt(Math.Pow((double)_virtualPos.x, 2.0d) + Math.Pow((double)_virtualPos.z, 2.0d));
                double dist = Math.Sqrt(Math.Pow((double)tempDist, 2.0d) + Math.Pow((double)_virtualPos.y - 1, 2.0d));
                float alpha = (float)Math.Acos(Math.Pow(dist, 2) / (4 * dist));
                float beta = (float)Math.Acos((Math.Pow(dist, 2.0d) - 8.0d) / -8.0d);
                float gamma = (float)Math.Atan((_virtualPos.y - 1) / tempDist);
                float epsilon = 0;

                if (_virtualPos.x > 0)
                {
                    epsilon = -(float)Math.Atan(_virtualPos.z / _virtualPos.x);
                }
                else if (_virtualPos.x == 0) { }
                else
                {
                    epsilon = -(M.DegreesToRadians(180) + (float)Math.Atan(_virtualPos.z / _virtualPos.x));
                }

                float delta = 0;

                float finalAlpha = 0;
                float finalBeta = 0;

                if (!float.IsNaN(alpha))
                {
                    finalAlpha = -(M.DegreesToRadians(90) - alpha - gamma);
                    finalBeta = -(M.DegreesToRadians(180) - beta);
                    delta = (M.DegreesToRadians(90) - finalAlpha - beta);
                }
                else
                {
                    finalAlpha = -(M.DegreesToRadians(90) - gamma);
                    delta = (M.DegreesToRadians(-90) - finalAlpha);
                }



                _lowerAxleTransform.Rotation = new float3(0, 0, finalAlpha);
                _middleAxleTransform.Rotation = new float3(0, 0, finalBeta);
                _upperAxleTransform.Rotation = new float3(0, 0, delta);
                _footTransform.Rotation = new float3(0, epsilon, 0);

                Diagnostics.Log("Coordinates: " + _virtualPos);
                Diagnostics.Log("Distance: " + dist);
                Diagnostics.Log("Alpha: " + M.RadiansToDegrees(alpha));
                Diagnostics.Log("Beta: " + M.RadiansToDegrees(beta));
                Diagnostics.Log("Gamma: " + M.RadiansToDegrees(gamma));
                Diagnostics.Log("Epsilon: " + M.RadiansToDegrees(epsilon));
                Diagnostics.Log("Delta: " + M.RadiansToDegrees(delta));
            }

            //Open/Close Pincer
            if (Keyboard.GetButton(79))
            {
                _move = true;
            }

            if (_move && _open)
            {
                if (_rightPincerTransform.Rotation.x < M.DegreesToRadians(0))
                {
                    _leftPincerTransform.Rotation -= new float3(1 * Time.DeltaTime, 0, 0);
                    _rightPincerTransform.Rotation += new float3(1 * Time.DeltaTime, 0, 0);
                }
                else if (_rightPincerTransform.Rotation.x >= M.DegreesToRadians(0))
                {
                    _move = false;
                    _open = false;
                }
            }
            else if (_move && !_open)
            {
                if (_rightPincerTransform.Rotation.x > M.DegreesToRadians(-45))
                {
                    _leftPincerTransform.Rotation += new float3(1 * Time.DeltaTime, 0, 0);
                    _rightPincerTransform.Rotation -= new float3(1 * Time.DeltaTime, 0, 0);
                }
                else if (_rightPincerTransform.Rotation.x <= M.DegreesToRadians(-45))
                {
                    _move = false;
                    _open = true;
                }
            }

            // Render the scene loaded in Init()
            _sceneRenderer.Render(RC);

            // Swap buffers: Show the contents of the backbuffer (containing the currently rendered frame) on the front buffer.
            Present();
        }

        private InputDevice Creator(IInputDeviceImp device)
        {
            throw new NotImplementedException();
        }

        // Is called when the window was resized
        public override void Resize()
        {
            // Set the new rendering area to the entire new windows size
            RC.Viewport(0, 0, Width, Height);

            // Create a new projection matrix generating undistorted images on the new aspect ratio.
            var aspectRatio = Width / (float)Height;

            // 0.25*PI Rad -> 45° Opening angle along the vertical direction. Horizontal opening angle is calculated based on the aspect ratio
            // Front clipping happens at 0.01 (Objects nearer than 1 world unit get clipped)
            // Back clipping happens at 200 (Anything further away from the camera than 200 world units gets clipped, polygons will be cut)
            var projection = float4x4.CreatePerspectiveFieldOfView(M.PiOver4, aspectRatio, 0.01f, 200.0f);
            RC.Projection = projection;
        }
    }
}
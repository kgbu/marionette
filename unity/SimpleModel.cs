﻿using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using live2d;
using live2d.framework;
using MiniJSON;



[ExecuteInEditMode]
public class SimpleModel : MonoBehaviour
{
    public TextAsset mocFile;
    public TextAsset physicsFile;
    public Texture2D[] textureFiles;

    public string SERIAL_PORT = "COM5";
    public int SERIAL_BAUD_RATE = 115200;
    private const int SERIAL_TIMEOUT = 500;

    private Thread _readThread;
    private static SerialPort _serialPort;
    private static bool _continue;
    private static Int64 counterloop = 0;

    private float gyX, gyY, gyZ;
    private float _lastGyX, _lastGyY, _lastGyZ;
    private float acX, acY, acZ;

    private string lastline;

    private Live2DModelUnity live2DModel;
    private EyeBlinkMotion eyeBlink = new EyeBlinkMotion();
    private L2DTargetPoint dragMgr = new L2DTargetPoint();
    private L2DPhysics physics;
    private Matrix4x4 live2DCanvasPos;



    void Start()
    {
        Live2D.init();

        load();


        _readThread = new Thread(Read);
        _serialPort = new SerialPort(SERIAL_PORT, SERIAL_BAUD_RATE);
        _serialPort.ReadTimeout = SERIAL_TIMEOUT;
        _serialPort.Open();
        _continue = true;
        _readThread.Start();
        lastline = "";
        _lastGyX = _lastGyY = _lastGyZ = 0.0F;
    }


    void load()
    {
        live2DModel = Live2DModelUnity.loadModel(mocFile.bytes);

        for (int i = 0; i < textureFiles.Length; i++)
        {
            live2DModel.setTexture(i, textureFiles[i]);
        }

        float modelWidth = live2DModel.getCanvasWidth();
        live2DCanvasPos = Matrix4x4.Ortho(0, modelWidth, modelWidth, 0, -50.0f, 50.0f);

        if (physicsFile != null) physics = L2DPhysics.load(physicsFile.bytes);
    }


    void Update()
    {
        if (live2DModel == null) load();
        live2DModel.setMatrix(transform.localToWorldMatrix * live2DCanvasPos);
        if (!Application.isPlaying)
        {
            live2DModel.update();
            return;
        }
        Debug.Log(lastline);
        Debug.Log(counterloop);
        Debug.Log(_continue);

        var pos = Input.mousePosition;
        if (Input.GetMouseButtonDown(0))
        {
            //
        }
        else if (Input.GetMouseButton(0))
        {
            dragMgr.Set(pos.x / Screen.width * 2 - 1, pos.y / Screen.height * 2 - 1);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            dragMgr.Set(0, 0);
        }


        dragMgr.update();
        live2DModel.setParamFloat("PARAM_ANGLE_X", acX);
        live2DModel.setParamFloat("PARAM_ANGLE_Y", acY);

        live2DModel.setParamFloat("PARAM_BODY_ANGLE_X", gyZ);

        live2DModel.setParamFloat("PARAM_EYE_BALL_X", gyX);
        live2DModel.setParamFloat("PARAM_EYE_BALL_Y", gyY);

        double timeSec = UtSystem.getUserTimeMSec() / 1000.0;
        live2DModel.setParamFloat("PARAM_BREATH", acZ);

        eyeBlink.setParam(live2DModel);

        if (physics != null) physics.updateParam(live2DModel);

        live2DModel.update();
    }

    void OnApplicationQuit()
    {
        _continue = false;
        _readThread.Join();
        _serialPort.Close();
    }


    void OnRenderObject()
    {
        if (live2DModel == null) load();
        if (live2DModel.getRenderMode() == Live2D.L2D_RENDER_DRAW_MESH_NOW) live2DModel.draw();
    }

    private void Read()
    {
        string jsonLine;
        while (_continue)
        {
            if (_serialPort.IsOpen)
            {
                try
                {
                    jsonLine = _serialPort.ReadLine();
                    lastline = jsonLine;
                    counterloop++;
                    var json = MiniJSON.Json.Deserialize(jsonLine) as Dictionary <string, float> ;
                    acX = (float) Math.Sqrt(json["AcX"] / 20000.0F);
                    acY = (float) Math.Sqrt(json["AcY"] / 20000.0F);
                    acZ = (float) Math.Sqrt(json["AcZ"] / 20000.0F);
                    gyX = ((float)json["GyX"]) - _lastGyX;
                    _lastGyX = ((float)json["GyX"]);
                    gyY = ((float)json["GyY"]) - _lastGyY;
                    _lastGyY = ((float)json["GyY"]);
                    gyZ = ((float)json["GyX"]) - _lastGyZ;
                    _lastGyZ = ((float)json["GyX"]);
                }
                catch (TimeoutException)
                {
                    lastline = "timeout exception";
                }
            } else
            {
                lastline = "port is closed";
            }
            Thread.Sleep(0);
        }
    }
}
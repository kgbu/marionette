﻿using UnityEngine;
using System;
using System.IO.Ports;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using live2d;
using live2d.framework;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;



[ExecuteInEditMode]
public class SimpleModel : MonoBehaviour
{
   
    public TextAsset mocFile;
    public TextAsset physicsFile;
    public Texture2D[] textureFiles;

    private MqttClient4Unity client;

    public string brokerHostname;
    public int brokerPort = 1883;
    public string userName = "";
    public string password = "";
    public string topic = "";
    public float AccRatio = 2000.0F;
    public float GyroRatio = 7000.0F;


    private float gyX, gyY, gyZ;
    private const Int64 mean_size = 20;
    private float[] _lastGyX = new float[20];
    private float[] _lastGyY = new float[20];
    private float[] _lastGyZ = new float[20];
    private float acX, acY, acZ;

    private Int64 meanindex = 0;

    private Live2DModelUnity live2DModel;
    private EyeBlinkMotion eyeBlink = new EyeBlinkMotion();
    private L2DPhysics physics;
    private Matrix4x4 live2DCanvasPos;



    void Start()
    {
        Live2D.init();

        load();

        if (brokerHostname != null && userName != null && password != null)
        {
            Connect();
            client.Subscribe(topic);
        }

        var j = (IDictionary) MiniJSON.Json.Deserialize("{\"AcX\": -15200, \"AcY\": -1416, \"AcZ\": 4292, \"Tmp\": -4528, \"GyX\": -203, \"GyY\": 72, \"GyZ\": -48}");
        Debug.Log(j.GetType());
        
    }

    void Connect()
    {
        // SSL使用時はtrue、CAを指定
        client = new MqttClient4Unity(brokerHostname, brokerPort, false,
                                      null);
        // clientidを生成
        string clientId = Guid.NewGuid().ToString();
        client.Connect(clientId, userName, password);
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
        while (client.Count() > 0)
        {
            meanindex = (meanindex + 1) % mean_size;
            string jsonLine = client.Receive().Split('\n')[0];
            var json = MiniJSON.Json.Deserialize(jsonLine) as Dictionary<string, object>;
            Debug.Log(jsonLine);

            if (json == null) { continue;  }
            Debug.Log(json["AcX"].GetType() + "type of json");
            Debug.Log(json["AcX"]);
            acX = (Int64)json["AcX"] / AccRatio;
            acY = (Int64)json["AcY"] / AccRatio;
            acZ = (Int64)json["AcZ"] / AccRatio;
            _lastGyX[meanindex] = ((Int64)json["GyX"]);
            _lastGyY[meanindex] = ((Int64)json["GyY"]);
            _lastGyZ[meanindex] = ((Int64)json["GyZ"]);
            gyX = gyY = gyZ = 0.0f;
            for (var i = 0; i < mean_size; i++)
            {
                gyX += _lastGyX[i];
                gyY += _lastGyY[i];
                gyY += _lastGyY[i];
            }
            gyX = gyX / GyroRatio;
            gyY = gyY / GyroRatio;
            gyZ = gyZ / GyroRatio;

            Debug.LogFormat("{0} {1} {2} {3} {4} {5}",acX, acY, acZ, gyX, gyY, gyZ);
            break;
        }
        

        live2DModel.setParamFloat("PARAM_ANGLE_X", acX); // head panning : value range -30.0 to 30.0 (degree)
        live2DModel.setParamFloat("PARAM_ANGLE_Y", acY); // head banking back and forth : -30 to 30 

        live2DModel.setParamFloat("PARAM_BODY_ANGLE_X", acZ); // body angle sideway : -30.0 to 30.0

        live2DModel.setParamFloat("PARAM_EYE_L_OPEN", gyX + 0.5f); // 0 to 1
        live2DModel.setParamFloat("PARAM_EYE_R_OPEN", gyX + 0.5f); // 

        live2DModel.setParamFloat("PARAM_BROW_L_Y", gyX); // -1.0 to 1.0
        live2DModel.setParamFloat("PARAM_BROW_R_Y", gyX); // 

        live2DModel.setParamFloat("PARAM_MOUTH_OPEN_Y", gyY + 0.5f); // 0 to 1.0
        live2DModel.setParamFloat("PARAM_MOUTH_FORM", gyZ); //  -1.0 to 1.0


        live2DModel.setParamFloat("PARAM_BREATH", 1);

        eyeBlink.setParam(live2DModel);

        if (physics != null) physics.updateParam(live2DModel);

        live2DModel.update();
    }



    void OnRenderObject()
    {
        if (live2DModel == null) load();
        if (live2DModel.getRenderMode() == Live2D.L2D_RENDER_DRAW_MESH_NOW) live2DModel.draw();
    }


}
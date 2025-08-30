using System.Collections;
using System.Collections.Generic;
using Test;
using UnityEngine;
using UnityEngine.UI;

public class Showstate : MonoBehaviour
{
    //绑定的UI
    public GameObject UIlayer;
    private Text stateText;    
    private Text speedText;    
    
    //show
    private string currentState;
    //属性
    public GameObject target;
    private MyTestController2 myTestController;

    void Start()
    {
        //UIlayer = GameObject.Find("UIlayer");
        
        // 通过名称查找特定的Text组件
        Text[] texts = UIlayer.GetComponentsInChildren<Text>();
        stateText = texts[0];
        speedText = texts[1];
        myTestController = target.GetComponent<MyTestController2>();
    }
    
    void Update()
    {
        if (myTestController != null)
        {
            // 更新状态文本
            currentState = myTestController.CurrentCharacterState.ToString();
            if (stateText != null)
            {
                stateText.text = "State: " + currentState;
            }
            
            // 更新速度文本
            if (speedText != null)
            {
                float speed = myTestController.Motor.Velocity.magnitude;
                speedText.text = "Speed: " + speed.ToString("F2");
            }
        }
    }
}
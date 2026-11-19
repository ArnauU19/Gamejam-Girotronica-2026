using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance {get; private set;}

    public enum controls
    {
        Null=0,
        wasd=1,
        // RLeft=2,
        // RRight=3,
        MPos=4,
        MWheel=5,
        RClick=6,
        LClick=7,
        KeySpace=8
    }

    Vector2 wasd;
    PlayerInput playerInput;
    InputAction wasdAction=null;

    // InputAction rotateLeft=null;
    // InputAction rotateRight=null;
    InputAction mousePos=null;
    InputAction mouseWheel=null;
    InputAction rightClick=null;
    InputAction leftClick=null;

    InputAction keyboardSpace=null;


    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Awake() {
        if (Instance == null)
        {
            Instance=this;
        }
        else
        {
            Destroy(gameObject);
        }
  
        if(!TryGetComponent<PlayerInput>(out playerInput))
        {
            Debug.LogError("NOT FIND PLAYER INPUT");
        }
        //Arnau: he comentat aquest codi perque el nostre inputMap no serà igual que el que tenia jo, així no trobaria les accions que buscava, ja modificaré segons les nostres accions

        /* wasdAction= playerInput.actions.FindAction("wasd");
        // rotateLeft=playerInput.actions.FindAction("RotateLeft");
        // rotateRight=playerInput.actions.FindAction("RotateRight");
        mousePos=playerInput.actions.FindAction("MousePos");
        mouseWheel=playerInput.actions.FindAction("MouseWheel");
        rightClick=playerInput.actions.FindAction("RightClick");
        leftClick=playerInput.actions.FindAction("LeftClick");
        keyboardSpace=playerInput.actions.FindAction("KeyboardSpace"); */
    }
 

    //si un script vol buscar una acció, crida ReturnAction i subscriu el mètode a performed o el que vulgui
    public InputAction ReturnAction(controls thisControl)
    {
        InputAction res;
        switch (thisControl)
        {
            case controls.Null:
                Debug.LogError("controls null");
                res= null;
                break;
            case controls.wasd:
                res= wasdAction;
                break;
            /* case controls.RLeft:
                res= rotateLeft;
                break;
            
            case controls.RRight:
                res= rotateRight;
                break; */
            case controls.MPos:
                res= mousePos;
                break;
            case controls.MWheel:
                res= mouseWheel;
                break;
            case controls.RClick:
                res= rightClick;
                break;
            case controls.LClick:
                res= leftClick;
                break;
            case controls.KeySpace:
                res= keyboardSpace;
                break;
            default:
                Debug.LogError("Cas especific no concretat");
                res= null;
                break;
            
            /* MousePos
                Excepció: Aquest s'assigna al inspector, tant gameManager, com PlacementManager miren la posició del ratolí, canviar si necesari,
                o ampliar si fes falta
             */
        }

        if (res == null)
        {
            Debug.LogError("AAAAAAAAAAAAAAA INPUT ACTION NO S'HA ASSIGNAT BÉ");
        }
        return res;
    }
    


    #region Exclusive using inputs 
    // Basicament, això serveix per si tenim múltiples accions assignades a la mateixa tecla,
    //si volem que una acció s'apoderi d'una tecla crida IAmExclusiveUsing i si alguna altra la vol utilitzar que consulti SomeOneIsExclusiveUsing o AmIExclusiveUsing  
    private Dictionary<controls, GameObject> ExclusiveUsing= new Dictionary<controls, GameObject>();
    internal bool SomeOneIsExclusiveUsing(controls action, GameObject gameObject)
    {
        bool res=false;
        if(!ExclusiveUsing.ContainsKey(action )){
            Debug.LogError("exclusive using hauria de contenir totes les accions");
        }      
        // Torna false, si exclusiveUsing action es null o es el mateix gameobject
        if(ExclusiveUsing[action]==null/*  || ExclusiveUsing[action]==gameObject */){
            return false;
        }
        // Tornar true si algú diferent de gameobject està utilitzant exclusiveUsing action
        else{
            return true;
        }
    }
     internal bool AmIExclusiveUsing(controls action, GameObject gameObject)
    {
        if(!ExclusiveUsing.ContainsKey(action)){
            Debug.LogError("exclusive using hauria de contenir totes les accions");
        }        
        if(ExclusiveUsing[action]==gameObject){
            return true;
        }else{
            return false;
        }
        // ExclusiveUsing[action]=gameObject;
    }
    internal void IAmExclusiveUsing(controls action, GameObject gameObject)
    {
        if(!ExclusiveUsing.ContainsKey(action)){
            Debug.LogError("exclusive using hauria de contenir totes les accions");
        }        
        ExclusiveUsing[action]=gameObject;
    }
    internal void IAmNOTExclusiveUsing(controls action, GameObject gameObject)
    {
        if(!ExclusiveUsing.ContainsKey(action )){
            Debug.LogError("exclusive using hauria de contenir totes les accions");
        }      
        /* if(ExclusiveUsing[action]!=gameObject){
            Debug.LogError("exclusive using hauria de ser aquest gameobject, per això l'estem alliberant, algun error de flux");
        }else{ */
            ExclusiveUsing[action]=null;
        // }
    }

    void Start()
    {
        foreach (controls item in Enum.GetValues(typeof(controls)))
        {
            ExclusiveUsing.Add(item, null);
        }
        // ExclusiveUsing
    }
    #endregion
}

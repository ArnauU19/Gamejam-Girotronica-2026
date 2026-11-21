using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using UnityEngine.UI; 
public class AnimationChangeScreen : MonoBehaviour
{


    [SerializeField]bool ActivateFadeIn=false;
    [SerializeField]bool ActivateFadeOut=false;

    [SerializeField]private GameObject[] Frames;
    // [SerializeField]private UnityEngine.UI.Image canvas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // StartCoroutine(FadeOutTry());
        
    }

    // Update is called once per frame
    void Update()
    {
        if (ActivateFadeIn)
        {
            StartCoroutine(FadeInTry());
            ActivateFadeIn=false;
        }
        if (ActivateFadeOut)
        {
            StartCoroutine(FadeOutTry());
            ActivateFadeOut=false;
        }
    }

    IEnumerator FadeInTry()
    {
        GameObject ant=null;
        foreach (var item in Frames)
        {
            // canvas.sprite= item;
            item.gameObject.SetActive(true);
            if(ant!=null) ant.SetActive(false);
            ant=item;

            yield return new WaitForSeconds(0.1f);

        }
        yield return null;
    }
    IEnumerator FadeOutTry()
    {
        // List<GameObject> inverselist= Frames;
        // inverselist=Frames.inver 
        // foreach (var item in Frames)
        Frames.Last().SetActive(true);
        

        GameObject ant=null;
        int max=Frames.Count();
        for (int i = max-1; i >-1; i--)
        {
            // canvas.sprite= item;
            GameObject item= Frames[i]; 
            Debug.Log($"read {i}");
            item.gameObject.SetActive(true);
            if(ant!=null) ant.SetActive(false);
            ant=item;
            yield return new WaitForSeconds(0.1f);

        }
        ant.gameObject.SetActive(false);

        yield return null;
    }
}

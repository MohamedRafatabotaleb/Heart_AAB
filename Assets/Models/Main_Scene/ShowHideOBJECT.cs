using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowHideOBJECT : MonoBehaviour
{
    public GameObject ShowMain;

    public GameObject HideMain;

    public GameObject loopOBJ;

    public GameObject loopCircleAutoRotate;

    public string LoadLevelGO;

    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void MakeStringLevel(string LoadLevelFUN){

        LoadLevelGO = LoadLevelFUN;
    }

    public void ShowFUN(){

        ShowMain.SetActive(true);
    }

    public void HideFUN(){
                HideMain.SetActive(false);

    }

    public void ReverseFUN(){
          HideMain.SetActive(false);
         ShowMain.SetActive(true);
        loopOBJ.GetComponent<ResetModel>().mrtest= "";

    }

     public void ReverseLoadMainScene(){

         if(LoadLevelGO == "Main"){

            Application.LoadLevel("1_Main");
         }

        if(LoadLevelGO == "Muscular"){

            Application.LoadLevel("2_MuscularSystem");
         }
         if(LoadLevelGO == "Upper"){

            Application.LoadLevel("3_Upperlimbs");
         }
         
        if(LoadLevelGO == "Urinary"){

            Application.LoadLevel("4_TheUrinarySystem");
         }
         if(LoadLevelGO == "Digestive"){

            Application.LoadLevel("5_DigestiveSystem");
         }
         
        if(LoadLevelGO == "Respiratory"){

            Application.LoadLevel("6_RespiratorySystem");
         }         
         if(LoadLevelGO == "Nervous"){

            Application.LoadLevel("7_NervousSystem");
         }  


    }

    public void loopCircleAutoRotateFUN(){

        loopCircleAutoRotate.GetComponent<Rot_OBJ_Automatic>().enabled = true;

    }


    public void ToMuscularSystem()
    {
        Application.LoadLevel("2_MuscularSystem");

    }

     public void ToUpperLimbs()
    {
        Application.LoadLevel("3_Upperlimbs");

    }

     public void ToTheUrinarySystem()
    {
        Application.LoadLevel("4_TheUrinarySystem");

    }

     public void ToDigestiveSystem()
    {
        Application.LoadLevel("5_DigestiveSystem");

    }

    public void ToRespiratorySystem()
    {
        Application.LoadLevel("6_RespiratorySystem");

    }

    public void ToNervousSystem()
    {
        Application.LoadLevel("7_NervousSystem");

    }

}

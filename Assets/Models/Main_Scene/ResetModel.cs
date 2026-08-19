using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class ResetModel : MonoBehaviour {


	[Header("--- Move Speed ---")] 
	[Space(10F)]

	public float moveSpeed = 10f;

    public float moveSpeedFAST = 10f;


    [Header("--- Rotate Speed ---")] 
	[Space(10F)]

	public float RotateSpeed = 10f;

    public float RotateSpeedFAST = 10f;



    [Header("--- Scale Speed ---")] 
	[Space(10F)]

	public float ScaleSpeed = 10f;

    public float ScaleSpeedFAST = 10f;


    public string mrtest = "M";

	[Header("--- Gameobject Selected ---")] 
	[Space(10F)]

	public GameObject OBJReset;


	public GameObject goto_Position_0;

	GameObject goto_Position_Out;

	void Start(){

	
		OBJReset = this.gameObject;
	}

	void Update ()
	{
		if (mrtest == "reset") {

				OBJReset.transform.rotation = Quaternion.Lerp (OBJReset.transform.rotation, goto_Position_0.transform.rotation, RotateSpeed * Time.deltaTime);

				OBJReset.transform.localScale = new Vector3 (Mathf.Lerp (OBJReset.transform.localScale.x, goto_Position_0.transform.localScale.x, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.y, goto_Position_0.transform.localScale.y, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.z, goto_Position_0.transform.localScale.z, ScaleSpeed * Time.deltaTime));


				OBJReset.transform.localPosition = new Vector3 (Mathf.Lerp (OBJReset.transform.localPosition.x, goto_Position_0.transform.localPosition.x, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.y, goto_Position_0.transform.localPosition.y, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.z, goto_Position_0.transform.localPosition.z, moveSpeed * Time.deltaTime));
		}


		if (mrtest == "main") {

				OBJReset.transform.rotation = Quaternion.Lerp (OBJReset.transform.rotation, goto_Position_0.transform.rotation, RotateSpeed * Time.deltaTime);

				OBJReset.transform.localScale = new Vector3 (Mathf.Lerp (OBJReset.transform.localScale.x, goto_Position_0.transform.localScale.x, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.y, goto_Position_0.transform.localScale.y, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.z, goto_Position_0.transform.localScale.z, ScaleSpeed * Time.deltaTime));


				OBJReset.transform.localPosition = new Vector3 (Mathf.Lerp (OBJReset.transform.localPosition.x, goto_Position_0.transform.localPosition.x, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.y, goto_Position_0.transform.localPosition.y, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.z, goto_Position_0.transform.localPosition.z, moveSpeed * Time.deltaTime));

					StartCoroutine(ToMain());

		}
        

		if (mrtest == "out") {

            OBJReset.transform.rotation = Quaternion.Lerp (OBJReset.transform.rotation, goto_Position_Out.transform.rotation, RotateSpeed * Time.deltaTime);


            OBJReset.transform.localScale = new Vector3 (Mathf.Lerp (OBJReset.transform.localScale.x, goto_Position_Out.transform.localScale.x, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.y, goto_Position_Out.transform.localScale.y, ScaleSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localScale.z, goto_Position_Out.transform.localScale.z, ScaleSpeed * Time.deltaTime));


				OBJReset.transform.localPosition = new Vector3 (Mathf.Lerp (OBJReset.transform.localPosition.x, goto_Position_Out.transform.localPosition.x, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.y, goto_Position_Out.transform.localPosition.y, moveSpeed * Time.deltaTime),

					Mathf.Lerp (OBJReset.transform.localPosition.z, goto_Position_Out.transform.localPosition.z, moveSpeed * Time.deltaTime));
		}

        if (mrtest == "resetFAST")
        {

            OBJReset.transform.rotation = Quaternion.Lerp(OBJReset.transform.rotation, goto_Position_Out.transform.rotation, RotateSpeedFAST * Time.deltaTime);


            OBJReset.transform.localScale = new Vector3(Mathf.Lerp(OBJReset.transform.localScale.x, goto_Position_Out.transform.localScale.x, ScaleSpeedFAST * Time.deltaTime),

                    Mathf.Lerp(OBJReset.transform.localScale.y, goto_Position_Out.transform.localScale.y, ScaleSpeedFAST * Time.deltaTime),

                    Mathf.Lerp(OBJReset.transform.localScale.z, goto_Position_Out.transform.localScale.z, ScaleSpeedFAST * Time.deltaTime));


            OBJReset.transform.localPosition = new Vector3(Mathf.Lerp(OBJReset.transform.localPosition.x, goto_Position_Out.transform.localPosition.x, moveSpeedFAST * Time.deltaTime),

                Mathf.Lerp(OBJReset.transform.localPosition.y, goto_Position_Out.transform.localPosition.y, moveSpeedFAST * Time.deltaTime),

                Mathf.Lerp(OBJReset.transform.localPosition.z, goto_Position_Out.transform.localPosition.z, moveSpeedFAST * Time.deltaTime));
        }


    }

	
IEnumerator ToMain()
    {
        Debug.Log("coroutineB created");

        yield return new WaitForSeconds(0.6f);

        Application.LoadLevel("1_Main");

        Debug.Log("coroutineB enables coroutineA to run");
    }


	public void MouseDown(string No_goto) {

		mrtest = No_goto;

		Debug.Log (mrtest);
	}

	public void MouseDownOut(GameObject gotoOUT) {

        goto_Position_Out = gotoOUT;

		mrtest = "out";

		Debug.Log (mrtest);
	}


    public void MouseDownOutFast(GameObject gotoOUTFAST)
    {

        goto_Position_Out = gotoOUTFAST;

        mrtest = "resetFAST";

        Debug.Log(mrtest);
    }

    public void Mouse_up() {

		mrtest = "";
	}
	
}
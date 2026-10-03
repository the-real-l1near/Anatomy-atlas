using UnityEngine;

public class TabBackgroundManager : MonoBehaviour
{
    [SerializeField] private GameObject lexiconTabBG;
    [SerializeField] private GameObject descriptionTabBG;
    [SerializeField] private GameObject helpTabBG;
    [SerializeField] private GameObject settingsTabBG;

    private void Update()
    {
        if (PanelsManagement.instance == null)
            return;

        lexiconTabBG.SetActive(PanelsManagement.instance.lex.isExpanded);
        descriptionTabBG.SetActive(PanelsManagement.instance.desc.isExpanded);
        helpTabBG.SetActive(PanelsManagement.instance.help.isExpanded);
    }
}
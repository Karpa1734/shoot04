using KanKikuchi.AudioManager;
using System.Collections;
using UnityEngine;

public class Emitter_Sloth : PlayerDanmakuEmitter
{

    protected override IEnumerator ExecuteSkillZ(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteZ(s));
    }
    protected override IEnumerator ExecuteSkillX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteX(s));
    }

    protected override IEnumerator ExecuteSkillC(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteC(s));
    }

    protected override IEnumerator ExecuteSkillV(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteV(s));
    }

    protected override IEnumerator ExecuteSkillEX(PlayerSkillData.SkillSettings s)
    {
        yield return StartCoroutine(SkillTempleteEX(s));
    }


    protected IEnumerator SkillTempleteZ(PlayerSkillData.SkillSettings s)
    {
        yield return null;
    }
    protected IEnumerator SkillTempleteX(PlayerSkillData.SkillSettings s)
    {

        yield return null;
    }
    protected IEnumerator SkillTempleteC(PlayerSkillData.SkillSettings s)
    {

        yield return null;
    }
    private IEnumerator SkillTempleteV(PlayerSkillData.SkillSettings s)
    {

        yield return null;
    }
    protected IEnumerator SkillTempleteEX(PlayerSkillData.SkillSettings s)
    {

        yield return null;
    }
}

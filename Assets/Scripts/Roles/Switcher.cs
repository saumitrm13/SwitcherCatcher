using UnityEngine;

public class Switcher : Role
{
    private Pole OwnedPole = null;
    private Pole TargetPole = null;
    private ulong clientID = 0;
    private Pole currentOccupiedPole = null;
    private SwitcherScript switcherScriptRef = null;
    public bool isCurrentlyAGuest = false;
    private bool isDead = false;
    public  Switcher(ulong clientID, SwitcherScript switcherScriptRef)
    {
        RoleType = RoleType.Switcher;
        this.clientID = clientID;
        this.switcherScriptRef = switcherScriptRef;
        isDead = false;
    }

    public void AssignPole(Pole pole)
    {
        OwnedPole = pole;
        currentOccupiedPole = pole;
    }

    public bool OwnsAPole()
    {   
        return OwnedPole != null;
    }

    public PoleType getOwnedPoleType() { 
        return OwnedPole.Type;   
    }
    public Pole getOwnedPole()
    {
        return OwnedPole;
    }
    public PoleType getTargetPoleType() { 
        return TargetPole.Type;
    }

    public void AssignTargetPole(Pole target)
    {   
         TargetPole = target;
    }

    public bool NeedsResources()
    {
        return TargetPole != null;  
    }

    public ulong getClientID() { 
        return clientID;
    }

    public void SetCurrentOccupiedPole(Pole currentPole)
    {
        currentOccupiedPole = currentPole;
    }

    public void FreeCurrentOccupiedPole()
    {
        currentOccupiedPole = null;
    }

    public bool hasOccupiedAPole()
    {
        return currentOccupiedPole != null;
    }

    public void ChangeOwnedPole(Pole newPole)
    {
        OwnedPole = newPole;
    }

    public SwitcherScript getSwitcherScriptRef()
    {
        return switcherScriptRef;
    }

    public Pole GetOwnedPole() { return OwnedPole; }

    public void Eliminate()
    {
        OwnedPole = null;
        TargetPole = null;
        currentOccupiedPole = null;
        RoleType = RoleType.None;
        Kill();
    }

    public void Kill()
    {
        isDead = true;
    }

    public bool IsDead()
    {
        return isDead;
    }
}

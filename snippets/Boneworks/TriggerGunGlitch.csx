var gun = Paste() as StressLevelZero.Props.Weapons.Gun;
var mag = gun.magazineSocket;
mag.MagazineRelease();
mag._magazinePlug._lastSocket = null;
mag.proxyGrip.ForceDetach();
mag.Unlock();

import * as THREE from "three";

export const createHeadset = () => {
  return new THREE.Mesh(
    new THREE.BoxGeometry(0.25, 0.2, 0.15),
    new THREE.MeshPhongMaterial({
      color: "#eeeeee",
    })
  );
};

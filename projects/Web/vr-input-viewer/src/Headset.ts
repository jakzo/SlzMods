import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader";

export const createHeadset = () => {
  const group = new THREE.Group();
  const loader = new GLTFLoader();
  loader.load(
    "/quest2_hmd.glb",
    (gltf) => {
      // TODO: Bake this rotation into the model
      gltf.scene.rotateX(Math.PI / -2);
      gltf.scene.rotateY(Math.PI);
      group.add(gltf.scene);
    },
    undefined,
    (err) => {
      console.error("Failed to load HMD model:", err);
    }
  );
  return group;
};

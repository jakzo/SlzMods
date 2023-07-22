import * as THREE from "three";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader";

export const createHeadset = () => {
  const group = new THREE.Group();
  const loader = new GLTFLoader();
  loader.load(
    "/quest2_hmd.glb",
    (gltf) => {
      group.add(gltf.scene);
    },
    undefined,
    (err) => {
      console.error("Failed to load HMD model:", err);
    }
  );
  return group;
};

Simple unity movement scripts including camera movement, keyboard movement and sliding mechanic 

Camera movement includes well camera movement. For camera holder assign parent of MainCamera (empty object), for orientation assign an empty object that is not related with camera but on player object, 
and for tilt holder assign another parent of MainCamera (also and empty object). Also I include a screenshot for reference.

For the player movement just asign orientation from the camera object from eariel, Rigidbody from player object and make special layer for what player can walk on and name it Ground or somethin similiar.
You also need to have Player input Handler script in the same folder or just assign it in inspector.

For the sliding script just assign Orientation and CameraHolder objects from camera.


Postać - empty player object
Ciało - Player body object //not important
also the other object like CameraKolor and Camera broń etc are not important for those scripts to work


<img width="257" height="168" alt="image" src="https://github.com/user-attachments/assets/a725f9ca-96eb-4aba-8b6a-6633f2a8ab64" />


Simple unity fps movement scripts including camera movement, keyboard movement and sliding mechanic with adjustable movement parameters
also Input Manager included.

Camera movement includes well camera movement. For camera holder assign parent of MainCamera (empty object), for orientation assign an empty object that is not related with camera but on player object, 
and for tilt holder assign another parent of MainCamera (also and empty object). Also I include a screenshot for reference.

For the player movement just asign orientation from the camera object from eariel, Rigidbody from player object and make special layer for what player can walk on and name it Ground or somethin similiar.
You also need to have Player input Handler script in the same folder or just assign it in inspector.

For the sliding script just assign Orientation and CameraHolder objects from camera.


Postać - empty player object
Ciało - Player body object //not important
also the other object like CameraKolor and Camera broń etc are not important for those scripts to work


<img width="257" height="168" alt="image" src="https://github.com/user-attachments/assets/a725f9ca-96eb-4aba-8b6a-6633f2a8ab64" />



<img width="456" height="343" alt="image" src="https://github.com/user-attachments/assets/99b0d71b-0ff9-45c2-8609-57b9f6c7d047" />




<img width="447" height="753" alt="image" src="https://github.com/user-attachments/assets/fe79b598-37c8-49ec-86ec-b4435ea9121e" />



<img width="451" height="837" alt="image" src="https://github.com/user-attachments/assets/8bf6c6d5-b4d4-4d48-87f2-8b0a3b77169c" />

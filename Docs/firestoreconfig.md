Rules for firestore

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {

    // Allow only users that are actually authenticated
    function signedIn() {
        return request.auth != null;
    }
    
    // WIP/Broken limit interaction to admin group
    function isAdmin() {
        return signedIn() &&         
        	'ADMIN'in get(/databases/$(database)/documents/users/$(request.auth.uid)).data.roles.values();
    }
    
    // allow editing your own folder
    function ownsData(userId) {
        return signedIn() && request.auth.uid == userId;
    }
    
    // allow editing your own key in a document
    function isSelf() {
    	    return signedIn() && request.auth.uid == resource.id;
    }
    
    // Prevent editing of role.
    function secRole() {
    	    return !request.resource.data.keys().hasAll(['role']) ||  request.resource.data.role == resource.data.role;
    }
    
    // Rules
    match /users/{userId} {
    	allow update, delete: if ownsData(userId) && secRole();
    	allow read: if ownsData(userId);
    	allow create: if signedIn() && secRole();
    
    
    	match /characters/{document=**} {
    		allow read, update, delete: if ownsData(userId);
    		allow create: if signedIn();
    }}


    // Part of website that is going to be removed
    match /messages/{messageId} {
      allow list: if isAdmin();
    	allow get, update, delete: if isSelf() || isAdmin();
    	allow create: if signedIn();
    }
  }
}

```
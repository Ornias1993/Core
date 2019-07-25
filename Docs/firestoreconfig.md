Rules for firestore

```
rules_version = '2';
// Allow read/write access on all documents to any user signed in to the application
service cloud.firestore {
  match /databases/{database}/documents {

    // Custom functions
    function signedIn() {
        return request.auth != null;
    }
    
    function isAdmin() {
        return signedIn() &&         
        	'ADMIN'in get(/databases/$(database)/documents/users/$(request.auth.uid)).data.roles.values();
    }
    
    function ownsMessage() {
        return signedIn() && request.auth.uid == resource.data.userId;
    }
    

    function isSelf() {
    	    return signedIn() && request.auth.uid == resource.id;
    }
    
    // Rules
    match /users/{userId} {
    	allow read, update, delete: if isSelf();
    	allow create: if signedIn();
    }
    
    match /messages/{messageId} {
      allow list: if isAdmin();
    	allow get, update, delete: if isSelf() || isAdmin();
    	allow create: if signedIn();
    }
  }
}

```
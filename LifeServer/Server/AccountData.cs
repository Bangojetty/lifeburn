namespace Server; 

public class AccountData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }
    public string email { get; set; }
    public string hashedPassword { get; set; }


    public AccountData(int id = 0, string displayName = "", string username = "", string email = "", 
        string hashedPassword = "") {
        this.id = id;
        this.displayName = displayName;
        this.username = username;
        this.email = email;
        this.hashedPassword = hashedPassword;
    }

    public override string ToString() {
        return $"{nameof(id)}: {id}\n{nameof(displayName)}: {displayName}\n{nameof(username)}: {username}\n{nameof(email)}: {email}";
    }
}

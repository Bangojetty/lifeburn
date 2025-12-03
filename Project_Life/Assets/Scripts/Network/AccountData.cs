
public class AccountData {
    public int id { get; set; }
    public string displayName { get; set; }
    public string username { get; set; }
    public string email { get; set; }
    public string hashedPassword { get; }


    public AccountData(int id, string displayName, string username, string email, string hashedPassword) {
        this.id = id;
        this.displayName = displayName;
        this.username = username;
        this.email = email;
        this.hashedPassword = hashedPassword;
    }

    public override string ToString() {
        return $"{nameof(id)}: {id}, {nameof(displayName)}: {displayName}, {nameof(username)}: {username}, {nameof(email)}: {email}, {nameof(hashedPassword)}: {hashedPassword}";
    }
}



package subpackage

const Const = ""

var Var string

type Interface interface{}

type Client struct{}

func NewClient() Client {
	return Client{}
}

func (c *Client) Foo() {}

type OtherClient struct{}

func NewOtherClient() OtherClient {
	return OtherClient{}
}

func (c *OtherClient) Foo() {}

func Foo() {}

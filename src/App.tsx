import { useState, useEffect } from 'react';
import './App.css';
import { Container, Row, Col, Alert, Button, Form, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

enum EQClass {
    Bard = 'Bard',
    Beastlord = 'Beastlord',
    Beserker = 'Beserker',
    Cleric = 'Cleric',
    Druid = 'Druid',
    Enchanter = 'Enchanter',
    Magician = 'Magician',
    Monk = 'Monk',
    Necromancer = 'Necromancer',
    Paladin = 'Paladin',
    Ranger = 'Ranger',
    Rogue = 'Rogue',
    Shadowknight = 'Shadowknight',
    Shaman = 'Shaman',
    Warrior = 'Warrior',
    Wizard = 'Wizard',
}

const api = window.location.protocol + '//' + window.location.hostname + ':3000';
const name = localStorage.getItem('name');
const classes = [
    EQClass.Bard,
    EQClass.Beastlord,
    EQClass.Beserker,
    EQClass.Cleric,
    EQClass.Druid,
    EQClass.Enchanter,
    EQClass.Magician,
    EQClass.Monk,
    EQClass.Necromancer,
    EQClass.Paladin,
    EQClass.Ranger,
    EQClass.Rogue,
    EQClass.Shadowknight,
    EQClass.Shaman,
    EQClass.Warrior,
    EQClass.Wizard,
];

function App() {
    const [isReady, setIsReady] = useState(name != null);
    const [mainName, setMainName] = useState(name || '');
    const [requests, setRequests] = useState<ILootRequest[]>([]);
    const [loots, setLoots] = useState<ILoot[]>([]);
    const start = async () => {
        localStorage.setItem('name', mainName);
        setIsReady(true);

        const res = await axios.get<ILootRequest[]>('/GetLootRequests');
        setRequests(res.data);

        await getLoots();
    };
    const getLoots = async () => {
        const res = await axios.get<ILoot[]>('/GetLoots');
        setLoots(res.data);
    };
    const logout = () => {
        localStorage.removeItem('name');
        setMainName('');
        setIsReady(false);
    };

    //useEffect(() => {
    //    (async () => {
    //        await axios.get<ILootRequest>('/user');
    //    })();
    //}, []);


    const deleteLootRequest = async (id: Number) => {
        await axios.post(api + '/DeleteLootRequest?id=' + id);
        setRequests(requests.filter(x => x.Id !== id));
    };
    const createLootRequest = async () => {
        const data = {
            MainName: '',
            CharacterName: '',
            Class: '',
            LootId: '',
            Quantity: '',
        };
        await axios.post(api + '/CreateLootRequest', data);
        // reset form fields?
    };

    return (
        <Container fluid>
            <Row>
                <Col>
                    <h1>KOI Raid Loot Tool (BETA)</h1>
                    {!isReady &&
                        <Alert variant={'primary'}>
                            <Alert.Heading>Let's Get Started!</Alert.Heading>
                            <p>Please enter your main character name</p>
                            <Form>
                                <Form.Control type="text" value={mainName} onChange={e => setMainName(e.target.value)} />
                            </Form>
                            <br />
                            <Button onClick={start} variant="primary">Start</Button>
                        </Alert>
                    }
                    {isReady &&
                        <>
                            <Row>
                                <Col md={{ span: 3, offset: 9 }}>
                                    <Alert variant='secondary'>
                                        Logged in as <strong>{mainName}</strong>
                                        <br />
                                        <Button onClick={logout}>Logout</Button>
                                    </Alert>
                                </Col>
                            </Row>
                            <Alert variant='primary'>
                                <h2>Create Loot Request</h2>
                                <Form>
                                    <Row>
                                        <Col>
                                            <Form.Group className="mb-3">
                                                <Form.Label>Character Name</Form.Label>
                                                <Form.Control type="text" placeholder="Enter name" />
                                            </Form.Group>
                                        </Col>
                                        <Col>
                                            <Form.Group className="mb-3">
                                                <Form.Label>Class</Form.Label>
                                                <Form.Select>
                                                    {classes.map((item, i) =>
                                                        <option value={item}>{item}</option>
                                                    )}
                                                </Form.Select>
                                            </Form.Group>
                                        </Col>
                                    </Row>
                                    <Row>
                                        <Col>
                                            <Form.Group className="mb-3">
                                                <Form.Label>Loot</Form.Label>
                                                {/*loot select*/}
                                            </Form.Group>
                                        </Col>
                                        <Col>
                                            <Form.Group className="mb-3">
                                                <Form.Label>Quantity</Form.Label>
                                                <Form.Control type="number" placeholder="Quantity" min="1" max="255" />
                                            </Form.Group>
                                        </Col>
                                    </Row>
                                    <Button variant='success' onClick={createLootRequest}>Create</Button>
                                </Form>
                            </Alert>
                            <h2>My Loot Requests</h2>
                            {requests.length === 0 &&
                                <Alert variant='warning'>You currently have zero requests</Alert>
                            }
                            {requests.length > 0 &&
                                <Table striped bordered hover size="sm">
                                    <thead>
                                        <tr>
                                            <th>Name</th>
                                            <th>Alt/Main</th>
                                            <th>Class</th>
                                            <th>Loot</th>
                                            <th>Quantity</th>
                                            <th></th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {requests.map((item, i) => {
                                            <tr key={item.Id}>
                                                <td>{item.CharacterName}</td>
                                                <td>{item.IsAlt ? 'Alt' : 'Main'}</td>
                                                <td>{item.Class}</td>
                                                <td>{item.LootId}</td>
                                                <td>{item.Quantity}</td>
                                                <td>
                                                    <Button variant='danger' onClick={() => deleteLootRequest(item.Id)}>Delete</Button>
                                                </td>
                                            </tr>
                                        })}
                                    </tbody>
                                </Table>
                            }
                        </>
                    }
                </Col>
            </Row>
        </Container>
    );
}

export default App;

interface ILoot {
    readonly Id: number;
    readonly Name: string;
    readonly Quantity: number;
}
interface ILootRequest {
    readonly Id: number;
    readonly CreatedDate: string;
    readonly MainName: string;
    readonly CharacterName: string;
    readonly Class: EQClass;
    readonly LootId: number;
    readonly Quantity: number;
    readonly IsAlt: boolean;
}

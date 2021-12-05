import { useState, useMemo } from 'react';
import './App.css';
import { Alert, Button, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = process.env.REACT_APP_API_PATH;

export function GrantedLoots(props: IContext) {

    const [isLoading, setIsLoading] = useState(false);

    const ungrantLootRequest = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/UngrantLootRequest?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    };
    const finishLootGranting = async () => {
        setIsLoading(true);
        try {
            await axios.post(api + '/FinishLootRequests');
        }
        finally {
            setIsLoading(false);
        }
    };

    const grantedLootRequests = useMemo(() =>
        props.requests.filter(x => x.granted),
        [props.requests]);

    return (
        <>
            <h3>Granted Loot Requests (Admin Only)</h3>
            {grantedLootRequests.length === 0 &&
                <Alert variant='warning'>There are currently zero granted loot requests</Alert>
            }
            {grantedLootRequests.length > 0 &&
                <>
                    <a target="_blank" rel="noreferrer" href={api + "/GetGrantedLootOutput"}>View Granted Loot Output Text (for discord)</a>
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
                            {grantedLootRequests.map((item, i) =>
                                <tr key={item.id}>
                                    <td><strong>{item.mainName}</strong> - {item.characterName}</td>
                                    <td>{item.isAlt ? 'Alt' : 'Main'}</td>
                                    <td>{classes[item.class as any]}</td>
                                    <td>{props.loots.find(x => x.id === item.lootId)?.name}</td>
                                    <td>{item.spell || item.quantity}</td>
                                    <td>
                                        <Button variant='danger' disabled={isLoading} onClick={() => ungrantLootRequest(item.id)}>Ungrant</Button>
                                    </td>
                                </tr>
                            )}
                        </tbody>
                    </Table>
                    <Alert variant={'warning'}>
                        <strong>WARNING!</strong> This deletes *ALL* loot requests and subtracts granted quantities. Only click this button once you have
                        granted all loots *AND* have parceled them out.
                        <br />
                        <Button variant={'success'} disabled={isLoading} onClick={finishLootGranting}>Finish Granting Loots</Button>
                    </Alert>
                </>
            }
        </>
    );
}
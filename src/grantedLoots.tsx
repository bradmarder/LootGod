import { useState, useMemo } from 'react';
import './App.css';
import { Alert, Button, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = window.location.protocol + '//' + window.location.hostname + ':5000';

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

    const grantedLootRequests = useMemo(() =>
        props.requests
            .filter(x => x.granted)
            .sort((a, b) => {
                const nameA = a.characterName.toLowerCase();
                const nameB = b.characterName.toLowerCase();
                if (nameA < nameB) { return -1; }
                if (nameA > nameB) { return 1; }
                return 0;
            }),
        [props.requests]);

    return (
        <>
            <h3>Granted Loot Requests (Admin Only)</h3>
            {grantedLootRequests.length === 0 &&
                <Alert variant='warning'>There are currently zero granted loot requests</Alert>
            }
            {grantedLootRequests.length > 0 &&
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
            }
        </>
    );
}